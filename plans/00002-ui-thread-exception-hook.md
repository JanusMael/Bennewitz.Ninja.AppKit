# 00002 — The UI-thread exception hook, and one filter for benign exceptions

> Status: **approved 2026-11-11**. Amends [`00001`](00001-entry-point-helper.md)'s decisions 6
> and 9; supersedes nothing. Asked for by ClaudeForge on 2026-11-10, the maintainer's decision of
> that day.

`AppMain` cannot hook Avalonia's dispatcher, since `AppServices.EntryPoint` references nothing. So
every Avalonia app still writes its own `Dispatcher.UIThread.UnhandledException` handler, and its own
test for which exceptions are noise. That contradicts the reason `EntryPointOptions()` exists: the
wiring lives in the package, so that a package bump fixes it in every app.

## The requirement

| # | From ClaudeForge's request |
|---|---|
| a | AppServices.Avalonia installs the `Dispatcher.UIThread.UnhandledException` handler: it logs, shows the fatal or non-fatal dialog, and decides `Handled` |
| b | A filter lets the host mark known-benign exceptions, such as the Linux D-Bus portal ones |
| c | The same filter feeds `EntryPointOptions()`'s unobserved-task handling, so the host no longer sets `OnUnobservedTaskException` to null |
| d | Check whether an unobserved task exception reaches stderr before the callback; if so, nulling the callback does not silence it |

## What exists

| Source | What it shows |
|---|---|
| `EntryRun.OnUnobservedTaskException` | Writes `warning: … had a task exception nobody observed` to stderr, then calls `OnUnobservedTaskException`. **Requirement d holds:** nulling the callback leaves the report on stderr |
| `AppMainOptions` | A `sealed class` with `init` properties, so `with` does not compile; ClaudeForge rebuilt the options by hand to change two of them. Its doc comment says `OnUnobservedTaskException` runs once the report is on stderr |
| `AvaloniaDiagnostics.InstallAvaloniaHooks()` | Called from `App.OnFrameworkInitializationCompleted`, once the dispatcher is live; installs `BindingValidationErrorLogger` only |
| `AvaloniaDiagnostics.EntryPointOptions()` | `OnFatal` logs fatal and shows the native dialog; `OnUnobservedTaskException` logs a warning; `FlushLog` is `Log.CloseAndFlush` |
| ClaudeForge's `App.axaml.cs` | Always sets `Handled`. A benign exception is logged at Debug, as is an `AggregateException` that is benign itself or whose inner exceptions all are. Anything else is logged at Error and shown in `FatalErrorDialog`, and the app keeps running. Its classifier matches the type name `Tmds.DBus.Protocol.DBusException`, plus, on Linux, the message prefixes `org.freedesktop.DBus.Error.` and `org.freedesktop.portal.` Its unobserved handler also counts all-cancellation exceptions as benign, and logs the rest at Error with the dialog |
| Other apps on this package | `bbavalonia` calls `InstallAvaloniaHooks()` and `EntryPointOptions()`, with no UI-thread handler of its own. OpenForge2k's copy of ClaudeForge has the same hand-written handler. DiffView's demo has one too, but against ClaudeForge's hand-packed `LayeredEditors.Avalonia.Diagnostics`, not this package |
| Both ClaudeForge copies' handler order | `InstallAvaloniaHooks()` runs before the app subscribes its own handler, so a handler the package installs there runs first. OpenForge2k pins `AppServicesVersion` at `2026.3.924` in `Directory.Build.props` and takes Dependabot's weekly NuGet updates, so a version bump can arrive on its own |
| Avalonia 12.1.3's `Dispatcher`, read from its decompiled source | Calls every `UnhandledException` subscriber with one shared args object. Only an exception from a `Post`ed job reaches the event; `Invoke` and `InvokeAsync` put it in the operation's task. It offers a given exception object to the handler once. Each headless `Session.Dispatch` gets a new dispatcher |
| `FatalErrorDialog.ShowSafe` | Opens a new modeless window on every call, with nothing stopping a second while the first is open. It swallows its own failures, writing them to stderr. Its `DefaultMessage` does not say the app will close |
| `BucketedRollingFileSink` | Writes every event with its full exception, and rolls a bucket's file at 4 MB, keeping Serilog.Sinks.File's default of 31 files. A flood therefore deletes the oldest lines first: the start of the session |
| DiffView's `FoldingOptionTests` and `EditUnderFoldTests` | Subscribe to `Dispatcher.UIThread.UnhandledException` under Avalonia.Headless and collect what it raises, so the event fires in a headless session and the hook can be tested there |
| `docs/appservices.md` | The AppServices family's packed README, the page nuget.org shows (`FamilyReadme` in every AppServices project), with an "Upgrading from …" section per breaking release |
| `bbavalonia`'s `--smoke` | Its releasing guide proves a release by running the binary with `--smoke` and checking it exits 0 (`templates/bbavalonia/docs/releasing.md`, and its `AGENTS.md`). A theme or binding failure on the UI thread fails that check today only because nothing handles it. Templates' own scripts never run `--smoke` |

## Decisions

| # | Decision | Why |
|---|---|---|
| 1 | **`InstallAvaloniaHooks()` installs the UI-thread handler, on by default** (the maintainer's choice, 2026-11-10), off with `AvaloniaDiagnosticsOptions.HandleUiThreadExceptions = false`. It installs it once, however often it is called. ClaudeForge is told what it must change, and that OpenForge2k's copy of ClaudeForge needs the same change later | Every app already calls it where the dispatcher is live, so `bbavalonia`'s apps, which have no handler today, get one from a package bump. **The cost, for an app with its own handler:** until it changes, the package's handler runs first, and it treats as non-benign whatever the app's own classifier calls benign, since the app has not yet set `IsBenignException`. So the Linux D-Bus portal exceptions, which show nothing today, are logged at Error and shown in a dialog, and real failures show two dialogs. There are three ways out: set `IsBenignException` and delete the app's own handler; set `HandleUiThreadExceptions = false`; or subscribe the app's own handler before calling `InstallAvaloniaHooks()`, after which decision 4 leaves alone what it handled. The version bump goes in the same commit as one of those, which is why OpenForge2k holds its version until it can do that. The upgrading note in `docs/appservices.md` says all of this. A handled exception no longer ends a `--smoke` run either, so `bbavalonia` sets `HandleUiThreadExceptions = !smoke` in the sync that takes this version (step 6) |
| 2 | **One filter, declared once: `AvaloniaDiagnosticsOptions.IsBenignException`**, a `Func<Exception, bool>?` (the maintainer's choice, 2026-11-10). The UI-thread handler reads it, and so do the options `EntryPointOptions()` returns. Those options are built before `ConfigureLogging` runs, so they read the filter only when an exception arrives. Before `ConfigureLogging` has run, nothing is benign. The filter is called on whichever thread raised the exception: the UI thread, or the finalizer thread for an unobserved task. So it must be quick and thread-safe, and its doc comment says so | The host writes its D-Bus classifier once, and it governs both paths. `EntryPointOptions()` keeps its signature, so apps already compiled against it still bind |
| 3 | **An exception is benign when the filter matches it.** An `AggregateException` is benign when it flattens to at least one exception and the filter matches all of them. A filter that throws counts as not benign, and its throw is reported beside the exception. **The rule has three users:** `EntryRun`, to decide stderr; and, in the Avalonia package, the UI-thread handler and `EntryPointOptions()`'s callback, which receives the exception without a verdict and must classify it again to choose its level. One copy lives in each package, and both copies are held to one shared test table | Unobserved task exceptions always arrive wrapped, so a filter written for `DBusException` would otherwise never match one. ClaudeForge's handler applies nearly this rule; it also passes the aggregate itself to its classifier, and counts an empty aggregate as benign. The Avalonia package does not reach an EntryPoint internal through the friend grants, since the two packages can resolve at different versions, and a renamed internal would then fail at run time |
| 4 | **The UI-thread handler sets `Handled` for every exception** (the maintainer's choice, 2026-11-10). A benign one is logged at Debug and shows nothing. Anything else is logged at Error and shown in a `FatalErrorDialog`, titled with `AppName` as `ShowFatalErrorDialog` titles it, and the app keeps running. The dialog is opened through an internal seam that reports when it closes, which `ShowSafe` cannot. The handler sets `Handled` before anything else, and nothing it does can throw out of it. It leaves alone an exception another handler has already handled. **One dialog at a time:** while a dialog it opened is open, a further non-benign exception is logged but not shown, and the gate reopens when that dialog closes or fails to show. **A repeat is logged once and never shown twice** (the maintainer's choice, 2026-11-11). A repeat is an exception with the type, message and throw site of one already reported. It is counted rather than logged, with one line giving its count when its dialog closes and when `EntryPointOptions()`'s `FlushLog` runs at exit | This is ClaudeForge's behaviour, with copyable details. An unhandled UI-thread exception would otherwise end the process from a click, where the user could still have saved their work. `ShowSafe` opens a new window on every call, so an exception that repeats on every frame would otherwise open windows without end. Logged in full on every frame, it would roll the log past its 31 files and delete the session's start, and a dialog that came back after every close could never be got rid of. A dialog that fails to show would otherwise keep the gate shut for the rest of the session, with nothing to say so |
| 5 | **`AppMainOptions.IsBenignException`**, a `Func<Exception, bool>?`, applied by decision 3's rule (the maintainer's choice, 2026-11-10; amends `00001`'s decision 6). A benign unobserved task exception is not written to stderr, but is still passed to `OnUnobservedTaskException`. A failure that ends the run is reported whatever the filter says | Requirement d. The callback still sees the exception, so the app chooses whether to log it, at what level. A predicate keeps EntryPoint free of references and AOT-safe |
| 6 | **`EntryPointOptions()` passes the filter on.** It sets `IsBenignException` from decision 2, and its `OnUnobservedTaskException` applies decision 3's rule, logging a benign exception at Debug and any other at Warning. It shows no dialog, as now (the maintainer's choice, 2026-11-10; amends `00001`'s decision 9) | Requirement c. By the time an unobserved exception is found, at a later garbage collection, its caller is gone, and a dialog then would offer the user nothing they can act on |
| 7 | **`AppMainOptions` becomes a `sealed record`** (the maintainer's choice, 2026-11-10), so a host changes one callback with `with { FlushLog = … }` | ClaudeForge rebuilt the options by hand to change `FlushLog`. The constructor and the properties keep their signatures, so compiled consumers still bind, which a package validation run measures. Equality becomes by value, and nothing in the family compares two options |

### The API

```csharp
// Bennewitz.Ninja.AppServices.EntryPoint
public sealed record AppMainOptions
{
    // …the four existing properties, unchanged…
    public Func<Exception, bool>? IsBenignException { get; init; }
}

// Bennewitz.Ninja.AppServices.AvaloniaUI
public sealed class AvaloniaDiagnosticsOptions
{
    // …
    public bool HandleUiThreadExceptions { get; init; } = true;
    public Func<Exception, bool>? IsBenignException { get; init; }
}
```

How ClaudeForge would use it, in the one commit that also takes the version and deletes its own
handlers:

```csharp
AvaloniaDiagnostics.ConfigureLogging(new AvaloniaDiagnosticsOptions
{
    AppName = "ClaudeForge",
    LogsDirectory = logs,
    // Cancellations are not added: on the UI thread ClaudeForge shows them today, and the one filter
    // governs both paths.
    IsBenignException = IsBenignLinuxInfrastructureException,
});

AppMainOptions defaults = AvaloniaDiagnostics.EntryPointOptions();
return AppMain.RunDesktop(typeof(Program).Assembly, args, Run, defaults with
{
    FlushLog = () => { Log.Information("Exiting ClaudeForge"); defaults.FlushLog?.Invoke(); },
});
```

### Dismissed

- **A separate opt-in call, `InstallUiThreadExceptionHandler()`.** It leaves existing apps alone, but
  `bbavalonia`'s apps would gain nothing from a package bump, which is the point of the request.
- **The filter as a parameter of both `InstallAvaloniaHooks` and `EntryPointOptions`.** Two places to
  pass the same predicate, and a changed `EntryPointOptions()` signature breaks compiled consumers.
- **Installing the handler from `EntryPointOptions()`.** It runs in `Main`, before the app is built,
  where the dispatcher is not yet the app's.
- **The D-Bus classifier in the package.** The request leaves classifying with the host. A later plan
  can move a ready-made predicate in once a second app needs the same one.
- **A dialog for unobserved task exceptions.** See decision 6. An app that wants one wraps the
  callback.
- **Decision 3's rule as a public helper in EntryPoint, shared by both packages.** One copy, but
  permanent API for an implementation detail; two copies held to one test table cost less.

## Scope

**In:** decisions 1 to 7, their tests, `docs/appservices.md`, the family's packed README (the hook, the
filter, and an "Upgrading from 2026.4.1110" note giving decision 1's cost and its three ways out), the
doc comment on `AppMainOptions.OnUnobservedTaskException`, the `AGENTS.md` rows that name them,
`PROGRESS.md`, a release of the AppServices family, and telling ClaudeForge and Templates.

**Out:** ClaudeForge's own change; the templates, which already call both methods, beyond the `--smoke`
line step 6 asks of Templates; a ready-made D-Bus classifier; ScopedEditors; `BNAQ1006`.

## Steps

| # | Step | Verified by |
|---|---|---|
| 1 | This plan, committed on its own once approved | CI green |
| 2 | EntryPoint: `AppMainOptions` as a record with `IsBenignException`, and its `OnUnobservedTaskException` doc comment; `EntryRun` writes nothing to stderr for a benign unobserved exception | `AppMainTests`, driven by decision 3's shared test table: a benign unobserved exception leaves stderr empty and still reaches the callback. A non-benign one is reported as before, and so is an aggregate with one non-benign inner exception. A throwing filter is reported along with the exception. A fatal exception is reported whatever the filter says. `with` compiles. A pack with `EnablePackageValidation` against `2026.4.1110` reports no breaking change. Each test seen red when broken on purpose, and the native AOT jobs stay green |
| 3 | Avalonia: `HandleUiThreadExceptions` and `IsBenignException`; the handler in `InstallAvaloniaHooks()`, with the dialog opened through an internal seam that reports when it closes; `ResetForTests` detaches the handler from the dispatcher it was installed on, and resets the gate and the counts; `EntryPointOptions()` passes the filter on | Headless tests. Each one installs, `Post`s a throwing action, runs `RunJobs()` and asserts, all inside one `Session.Dispatch`, with a fresh exception per throw, and with `MinimumLevel` at Debug wherever a Debug line is asserted. The cases: a non-benign exception is logged at Error, the dialog is requested, and `Handled` is true; a benign one is logged at Debug with no dialog; an aggregate follows decision 3's shared table. A second, different exception while the first dialog is open is logged and not shown, and one after that dialog closes is shown. A dialog that fails to show reopens the gate. A repeat is logged once, counted, never shown twice, and its count is written when its dialog closes and when `FlushLog` runs. An exception another handler already handled is left alone. A filter or a logger that throws does not escape the handler. `false` installs nothing, and two calls install one handler. `EntryPointOptions()`'s filter reads false before `ConfigureLogging` and the host's answer after it. Its callback, given an `AggregateException` wrapping a benign exception, logs at Debug, and one wrapping a non-benign exception logs at Warning. Each test seen red when broken on purpose |
| 4 | `docs/appservices.md`, with the upgrading note giving decision 1's cost and its three ways out; `src/AGENTS.md` and the root `AGENTS.md` invariants; `PROGRESS.md` | `repo-conventions check` conforms; CI green |
| 5 | Release the AppServices family at the day's version, on the maintainer's word, through `docs/publishing.md` | Every id in the nuget.org flat-container, then `PROGRESS.md`'s Published row |
| 6 | Tell ClaudeForge what it must change: in one commit, take the version, set its classifier as `IsBenignException`, and delete its UI-thread handler, its unobserved handler and its rebuilt options. Dropping its unobserved handler loses that handler's Error level and dialog, which a wrapped callback can keep. OpenForge2k's copy of ClaudeForge holds its version until it can make the same change, since Dependabot would otherwise bump it alone. Tell ClaudeForge the version once released. Tell Templates the version, and the one line `bbavalonia` needs in the same sync, `HandleUiThreadExceptions = !smoke`, without which a UI-thread failure passes its `--smoke` check | All told |
