# Bennewitz.Ninja.AppServices

Application services for desktop apps: shell launching, environment probing, sharing, dialogs and
logging, behind contracts a test can fake without referencing a UI toolkit. The launcher and the
share service report what actually happened, as a `LaunchResult` or a `ShareOutcome`, rather than
completing the same way whether or not anything did.

| Package | What it is |
|---|---|
| `Bennewitz.Ninja.AppServices.Abstractions` | The contracts and the dialog vocabulary: shell launching, environment, sharing, dialogs and pickers. Framework-free. |
| `Bennewitz.Ninja.AppServices` | Default implementations: shell launching, environment probing, sharing and a native error dialog. Needs an operating system, not a UI framework, so it works from a CLI, a service or a test. |
| `Bennewitz.Ninja.AppServices.Logging` | Serilog sinks for desktop applications, starting with a bucketed rolling file sink that keeps a bounded number of files per bucket. |
| `Bennewitz.Ninja.AppServices.Avalonia` | Avalonia implementations — dialog service, file pickers, fatal and non-fatal dialogs — the bridge that routes Avalonia's logger and its binding errors into Serilog, and a handler for the exceptions the UI thread lets escape. Its `EntryPointOptions` gives an Avalonia app's entry point the fatal dialog and the log. |
| `Bennewitz.Ninja.AppServices.EntryPoint` | One entry point for console, web and desktop apps: exit codes a script can read, a fatal report on stderr, Ctrl+C, `--version`, and the app's log flushed on every exit. References nothing but the framework, and is native AOT compatible. |

## Install

In an Avalonia application, one package brings the others with it:

```bash
dotnet add package Bennewitz.Ninja.AppServices.Avalonia
```

Without Avalonia, take `Bennewitz.Ninja.AppServices`, which brings the contracts, and add
`Bennewitz.Ninja.AppServices.Logging` if you want the sinks. Code that should only see the contracts
references `Bennewitz.Ninja.AppServices.Abstractions` alone.

Any app, console, web or desktop, takes `Bennewitz.Ninja.AppServices.EntryPoint` for its `Main`. It
references nothing else, so a native AOT app can take it too.

## Entry point

`AppMain` is the whole of an app's `Main`, with one method per kind of app:

```csharp
// A console app, which owns its command line
return await AppMain.RunConsoleAsync(typeof(Program).Assembly, args, RunAsync, new() { Usage = Usage });

// A web app: its arguments are configuration, and its host owns shutdown
return await AppMain.RunHostAsync(typeof(Program).Assembly, args, RunAsync);

// An Avalonia desktop app, with its fatal dialog and its log
return AppMain.RunDesktop(typeof(Program).Assembly, args, Run, AvaloniaDiagnostics.EntryPointOptions());
```

| Exit code | When |
|---|---|
| 0 | The work succeeded, or `--version` printed the release version |
| 1 | An exception escaped the work: it is reported on stderr, then passed to `OnFatal` |
| 2 | A console app's `UsageException`: its message and the usage go to stderr |
| 130 | Ctrl+C in a console app, which cancels the token its work was handed |

`--version`, as the only argument, prints the app's `[AssemblyMetadata("PublicVersion")]` before the
work runs. The fatal report is written to stderr before any callback, so it needs no logger, and
`FlushLog` flushes the app's own log on every exit. On SIGTERM, a console or desktop app flushes as
the signal arrives and then ends; a web app's host, once it is running, shuts down and ends the run,
which flushes. A run flushes only once, so an app that handles SIGTERM itself, with a generic host
for one, belongs in `RunHostAsync`. An exception on another thread is reported and flushed too, but
the process then ends with the runtime's own exit code. A `HostAbortedException`, which design-time
tools such as `dotnet ef` throw to stop an app, passes through untouched.

A task exception nobody observed does not end the run: it is reported on stderr with `warning:`,
then passed to `OnUnobservedTaskException`. `IsBenignException` marks the exceptions an app knows to
be harmless, and one it calls benign is kept off stderr and still passed to the callback. A filter
that throws counts as not benign, and its throw is reported on stderr beside the exception. A failure
that ends the run is reported whatever the filter says.

`AppMainOptions` is a record, so an app changes one member and keeps the rest:

```csharp
AppMainOptions defaults = AvaloniaDiagnostics.EntryPointOptions();
return AppMain.RunDesktop(typeof(Program).Assembly, args, Run, defaults with
{
    FlushLog = () => { Log.Information("Exiting"); defaults.FlushLog?.Invoke(); },
});
```

## Exceptions on the UI thread

In an Avalonia app, `AvaloniaDiagnostics.InstallAvaloniaHooks()`, called from
`OnFrameworkInitializationCompleted`, also installs a handler for every exception a job on the UI
thread lets escape, so a failure behind a click no longer ends the process. The handler marks each
exception handled:

| The exception | What happens |
|---|---|
| Benign, by `IsBenignException` | Logged at Debug, and nothing is shown |
| Any other | Logged at Error and shown in a `FatalErrorDialog` titled `Application Error — {AppName}`; the app keeps running |
| Another, while that dialog is open | Logged at Error, and not shown then; if it repeats, its first repeat after that dialog closes is shown |
| A repeat: the type, message and throw site of one already reported | Counted, never logged in full again, and never shown twice; a line gives its count when its dialog closes and when `EntryPointOptions()`' `FlushLog` runs |
| Avalonia's second report of any of these: the same exception, raised again at a later garbage collection as a task exception nobody observed | Neither logged nor written on stderr by `EntryPointOptions()`, so the handler's report is the only one |
| One a handler subscribed before the package's has already handled | Left alone, its second report included |

The handler is installed once, however often `InstallAvaloniaHooks()` is called, and
`HandleUiThreadExceptions = false` installs nothing.

`IsBenignException` is one filter for both paths: the handler reads it, and `EntryPointOptions()`
passes it on, so a benign task exception nobody observed is kept off stderr and logged at Debug,
where any other is logged at Warning. Set it where logging is configured:

```csharp
AvaloniaDiagnostics.ConfigureLogging(new AvaloniaDiagnosticsOptions
{
    AppName = "MyApp",
    LogsDirectory = logs,
    IsBenignException = IsKnownLinuxPortalNoise,
});
```

An `AggregateException` is benign when it flattens to at least one exception and the filter matches
every one of them. The aggregate itself is never passed to the filter, and a task exception nobody
observed always arrives as one, so a filter is written for the exceptions inside. A filter that
throws counts as not benign, and its throw is logged beside the exception. The filter runs on
whichever thread raised the exception, the UI thread or the finalizer thread, so it must be quick
and thread-safe. Until `ConfigureLogging` has run, nothing is benign.

## Upgrading from 2026.4.1110

- **`InstallAvaloniaHooks()` now handles the UI thread's unhandled exceptions, and does by
  default.** An app with a `Dispatcher.UIThread.UnhandledException` handler of its own, subscribed
  after `InstallAvaloniaHooks()`, runs both, the package's first, and the package's cannot know what
  the app's classifier calls benign. Until the app changes, the exceptions it ignores today, such as
  the Linux D-Bus portal ones, are logged at Error and shown in a dialog, and a real failure shows
  two dialogs. There are three ways out, and the version bump belongs in the same commit as one of
  them:
  1. set `AvaloniaDiagnosticsOptions.IsBenignException` to the app's classifier, and delete the
     app's own handler;
  2. set `HandleUiThreadExceptions = false`, and keep the app's own handler;
  3. subscribe the app's own handler before calling `InstallAvaloniaHooks()`: the package's then
     leaves alone every exception the app's handler marked handled.
- An exception the handler handles no longer ends the process, so a smoke run that proves a build by
  its exit code sets `HandleUiThreadExceptions = false` for that run.
- Setting `OnUnobservedTaskException` to null never kept a task exception off stderr, since the report
  is written before the callback runs. `IsBenignException` does, for the exceptions it calls benign.
- `AppMainOptions` is a `sealed record`. Code compiled against `2026.4.1110` still binds, `with` now
  compiles, and two options are equal when all their members are.

## Upgrading from 2026.3.924

- `DefaultShareService` has one public constructor, which takes nothing. The
  `Func<ProcessStartInfo, Process?>` it used to take as an optional test seam is internal now, so
  source that writes `new DefaultShareService()` compiles unchanged, but an assembly built against
  `2026.3.924` must be rebuilt: the constructor it calls is no longer public.

## Upgrading from 2026.3.923

- `Bennewitz.Ninja.AppServices.Avalonia` keeps its id, but the assembly inside it is now
  `AppServices.AvaloniaUI` and its namespaces are `Bennewitz.Ninja.AppServices.AvaloniaUI.*`, because a
  namespace segment named `Avalonia` shadows Avalonia's own root namespace.
- No `CancellationToken` parameter has a default any more; pass one. `IShareService.ShareTextAsync`'s
  `uri` is required as well (pass `null` for none), since a required token cannot follow an optional
  parameter.
- `ShareOutcome` gains `Cancelled`, returned when the token is already cancelled and nothing was
  attempted. A `switch` over the outcome needs the case.
- `AvaloniaDiagnosticsOptions` gains `ConfigureLogger` and `EventListener`, so a host can extend the
  log pipeline and receive diagnostic events without reaching into the library.

`2026.3.924` is also the first release whose assemblies are marked trimmable, with the trim analyser
running on every build.

`Bennewitz.Ninja.AppServices.Avalonia` `2026.3.924` requires **Avalonia 12.1.3 or later**. That
release fixes UI Automation selection never reaching the client on Windows
([AvaloniaUI/Avalonia#22151](https://github.com/AvaloniaUI/Avalonia/pull/22151)). A host that
references Avalonia directly at an earlier version fails restore with `NU1605`, a package downgrade,
until it raises that reference.

## Licence

MIT. See [LICENSE](https://github.com/JanusMael/Bennewitz.Ninja.AppKit/blob/main/LICENSE).
