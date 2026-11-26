# Bennewitz.Ninja.AppServices

Application services for desktop apps: shell launching, environment probing, sharing, dialogs and
logging, behind contracts a test can fake without referencing a UI toolkit. The launcher and the
share service report what actually happened, as a `LaunchResult` or a `ShareOutcome`, rather than
completing the same way whether or not anything did.

| Package | What it is |
|---|---|
| `Bennewitz.Ninja.AppServices.Abstractions` | The contracts and the dialog vocabulary: shell launching, environment, sharing, dialogs and pickers; and `LogRedaction`, which keeps a secret out of a log line. Framework-free. |
| `Bennewitz.Ninja.AppServices` | Default implementations: shell launching, environment probing, sharing and a native error dialog. Needs an operating system, not a UI framework, so it works from a CLI, a service or a test. |
| `Bennewitz.Ninja.AppServices.Logging` | Serilog sinks for desktop applications, starting with a bucketed rolling file sink that keeps a bounded number of files per bucket. |
| `Bennewitz.Ninja.AppServices.Avalonia` | Avalonia implementations — dialog service, file pickers, fatal and non-fatal dialogs — the bridge that routes Avalonia's logger and its binding errors into Serilog, and a handler for the exceptions the UI thread lets escape. Its `EntryPointOptions` gives an Avalonia app's entry point the fatal dialog, a dialog for a task exception nobody observed, and the log. |
| `Bennewitz.Ninja.AppServices.EntryPoint` | One entry point for console, web and desktop apps: exit codes a script can read, a fatal report on stderr, Ctrl+C, `--version`, a start and an exit line in the app's log, and the log flushed on every exit. References nothing but the framework, and is native AOT compatible. |

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
`FlushLog` flushes the app's own log on every exit. `WriteLogLine` writes a start line and an exit line
to that log, under "Log lines". On SIGTERM, a console or desktop app flushes as the signal arrives and
then ends; a web app's host, once it is running, shuts down and ends the run, which flushes. A run
flushes only once, so an app that handles SIGTERM itself, with a generic host for one, belongs in
`RunHostAsync`. An exception on another thread is reported and flushed too, but the process then ends
with the runtime's own exit code. A `HostAbortedException`, which design-time tools such as `dotnet ef`
throw to stop an app, passes through untouched.

A task exception nobody observed does not end the run: it is reported on stderr with `warning:`,
then passed to `OnUnobservedTaskException`. `IsBenignException` marks the exceptions an app knows to
be harmless, and one it calls benign is kept off stderr and still passed to the callback. A filter
that throws counts as not benign, and its throw is reported on stderr beside the exception. A failure
that ends the run is reported whatever the filter says.

`AppMainOptions` is a record, so an app changes one member and keeps the rest. An Avalonia app that
sets Serilog up in `Main` itself, without `ConfigureLogging`, replaces the writer of the entry point's
log lines, which would hold the start line for a log `ConfigureLogging` never makes (see "Log lines"):

```csharp
AppMainOptions defaults = AvaloniaDiagnostics.EntryPointOptions();
return AppMain.RunDesktop(typeof(Program).Assembly, args, Run, defaults with
{
    WriteLogLine = Log.Information,
});
```

## Log lines

Every line the packages write of their own takes the family's form, so that a line is found by its tag
and read without parsing a sentence: an `[Area.Kind]` tag, then camelCase `key=value` pairs, free text
quoted as `key="…"`, an enum by its name, and, but in a line of Avalonia's own, a Warning's, an Error's
or a Fatal's cause in `reason=`. A value the line carries, such as `count=12`, is a property named for
its key in PascalCase, `Count`; a word the line always writes, such as `reason=threw`, is part of its
message template. A value the package cannot read, such as a program's version or a failure's throw
site, is `unknown`. A quoted value is written as it is: a quote inside it is not escaped, and a stack or
an exception's text inside it spans lines, of which a search by tag finds the first. The sinks the
packages configure, the rolling files and the Trace sink, render `{Message:lj}`, and a sink an app adds
should render it too: under `{Message}`, Serilog quotes a string itself, so a value the line quotes is
quoted twice.

### The entry point's two lines

`AppMainOptions.WriteLogLine` writes two lines to the app's log, at Information:

| Line | When |
|---|---|
| `[App.Start] app=MyApp version=2026.4.1201 pid=4242` | Just before the work runs: the program assembly's name, its `PublicVersion` as `--version` prints it, or `unknown`, and the process's id |
| `[App.Exit] code=0 elapsedMs=1520` | When the work returns or throws, with the code `AppMain` returns; when the process exits before the run ends, as `Environment.Exit` makes it, with `Environment.ExitCode` |
| `[App.Exit] signal=SIGTERM elapsedMs=1520` | On SIGTERM in a console or desktop app, which the signal then ends |
| `[App.Exit] reason=unhandledException elapsedMs=1520` | When the runtime is ending the process on another thread's exception, after `OnFatal` |
| `[App.Exit] reason=hostAborted elapsedMs=1520` | When a design-time tool stops the host, whose `HostAbortedException` passes through |

A run offers its start line once, and writes one exit line, at the first of its ends and before
`FlushLog`; `elapsedMs` counts the whole milliseconds since `AppMain` was called. A run that answers
`--version` writes neither.

`WriteLogLine` is a `LogLineWriter`, a delegate that takes a message template and then its values in
order, as a logger's methods do, so the writer is a logger's method passed as it is: `Log.Information`
in a Serilog app, and `logger.LogInformation` in one with an `ILogger`. Serilog keeps the values as
properties, `Code` and `ElapsedMs` as numbers, and so does an `ILogger` over Serilog.Extensions.Logging.
A lambda around `logger.LogInformation` would hand it a template that is not a constant, which CA2254
flags as a suggestion; a method group is no call, so nothing does. A writer that formats a string, such
as `Console.Out.WriteLine`, binds too, and throws on the named holes; `AppMain` reports the throw on
stderr, as it reports any callback's.

```csharp
// Serilog, its logger set up in Main before AppMain
return await AppMain.RunConsoleAsync(typeof(Program).Assembly, args, RunAsync, new()
{
    Usage = Usage,
    WriteLogLine = Log.Information,
    FlushLog = Log.CloseAndFlush,
});

// An ILogger, from a factory made in Main before AppMain, which FlushLog disposes
ILoggerFactory factory = LoggerFactory.Create(builder => builder.AddConsole());
ILogger logger = factory.CreateLogger("MyApp");
return await AppMain.RunConsoleAsync(typeof(Program).Assembly, args, RunAsync, new()
{
    Usage = Usage,
    WriteLogLine = logger.LogInformation,
    FlushLog = factory.Dispose,
});
```

The start line is offered just before the work, so its log must be set up by then: a writer that
writes at once to a log the work sets up loses it. An app sets its logger up in `Main`, before
`AppMain`, as above; a failure setting it up there comes before `AppMain`, so the runtime reports it
and sets the exit code. An Avalonia app run with `EntryPointOptions()` that calls `ConfigureLogging` in
its run needs nothing more: the options' writer holds the start line until `ConfigureLogging` has made
the log, then writes it there as the log's first line, stamped as it is written, and drops it if the
run ends first, so that a run that never made its log leaves nothing for a later one. Before an exit
line it writes the repeat counts still pending, which the flush would otherwise write after it. An
Avalonia app that sets Serilog up in `Main` itself, without `ConfigureLogging`, passes `Log.Information`
instead, as under "Entry point": the options' writer would hold its start line for a log
`ConfigureLogging` never makes, and write its exit line alone.

A web app's exit line is written once its work has returned, after the host its work built is
disposed, so its log must outlive that host. Serilog's static logger does when `AddSerilog()` or
`UseSerilog()` is called with no logger and no configuration, the logger set up in `Main`, and
`FlushLog = Log.CloseAndFlush` flushes it after the exit line. An overload that configures the logger
inline, `AddSerilog(configuration => …)` or `UseSerilog((context, configuration) => …)`, gives the host
a logger it closes as it is disposed, and so does `dispose: true`: either way the exit line is lost.

### Secrets

A line an app writes with a value under a key that may name a secret passes the value through
`LogRedaction`, in `Bennewitz.Ninja.AppServices.Abstractions`, which references nothing, so that a
library that logs only through an `ILogger` can call it too:

```csharp
Log.Information("[Settings.Change] key={Key} value=\"{Value}\"", key, LogRedaction.Redact(key, value));
```

`Redact` gives `***`, `LogRedaction.Redacted`, for a sensitive key whatever the value, an empty one or
null included, so that a line never says whether a secret is set, and any other key's value as it is.
`IsSensitive` decides by the key alone. A key is sensitive when one of its dot-separated segments is
`env`, `headers`, `credentials`, `auth` or `authorization`, which makes `env.PATH` as sensitive as
`env.GITHUB_TOKEN`, or when it holds `token`, `secret`, `password`, `apikey`, `api_key`, `api-key`,
`bearer`, `private`, `accesskey`, `access_key` or `access-key` anywhere, case ignored in both. A segment
counts only whole, so `headers.Size` is sensitive and `headerSize` is not, and no key is sensitive for
merely holding `key`. Only a dot separates segments: `Credentials:User`, a configuration key, and
`Credentials__User`, an environment variable's name, are not sensitive, where `credentials.user` is. A
secret under a key that names none is written as it is.

### The packages' own lines

| Line | Level | Written |
|---|---|---|
| `[App.Fatal] reason=unhandledException` | Fatal | By `EntryPointOptions()`' `OnFatal`, with the exception that ended the run |
| `[UiThread.Exception] reason=unhandled benign=false` | Error | By the UI-thread handler, with the exception; one `IsBenignException` calls benign is `[UiThread.Exception] benign=true`, at Debug |
| `[UiThread.BenignFilter] reason=threw` | Warning | When `IsBenignException` threw on that exception, with its throw |
| `[Task.Unobserved] reason=notObserved benign=false` | Error, or Warning with `ShowUnobservedTaskExceptions = false` | For a task exception nobody observed, with it; a benign one is `[Task.Unobserved] benign=true`, at Debug |
| `[Task.BenignFilter] reason=threw` | Warning | When `IsBenignException` threw on that task exception, with its throw |
| `[Dialog.Fatal] reason=cannotShow` | Error | When a failure's dialog could not be shown, with the exception that stopped it; `reason=cannotPost` when it could not be posted to the UI thread |
| `[Failure.Repeat] reason=repeated count=12 type="…" message="…" site="…"` | Error | For a failure that repeated, its count since its last count line: when its dialog closes, before the exit line, and when `EntryPointOptions()`' `FlushLog` runs |
| `[App.Smoke] reason=unobservedTaskExceptions count=2 code=1` | Error | When `FailOnUnobservedTaskExceptions` fails a run |
| `[App.Smoke] reason=noExitEvent lifetime=…` | Warning | When `FailOnUnobservedTaskExceptions` is set and the app's lifetime raises no `Exit` |
| `[Dialog.Strings] key=LabelSave reason=threw` | Warning | The first time `DialogStrings.Resolver` fails for a key: `threw`, `gaveNull`, `gaveBlank` or `badPlaceholder` |
| `[Dialog.Copy] reason=clipboardFailed path="…"` | Warning | When copying to the clipboard failed, with the exception: a path, or `what=errorDetails` or `what=notice` in place of `path` |
| `[Binding.Validation] reason=exception control="…" dataContext="…" context="…" type="…" stack="…"` | Warning | By the binding validation logger, for a binding's exception: its type and a stack, never its message, which can hold what a person typed, and a text box's text as its length; an error that is no exception is `reason=error`, with `error="…"` |
| `[Avalonia.Log] area=Binding text="…"` | Avalonia's | For each message of Avalonia's that the bridge passes on, each value written as its own text; a failed conversion's text stops before the value it could not convert |

### A `DiagnosticSink`

For an operation that fails on an exception, `ShellLauncher` hands the `DiagnosticSink` it was given
one line in the form, with that exception: `[Shell.Launch] action=terminal reason=NotFound`, where
`action` is `terminal`, `reveal`, `editor` or `url`, and `reason` the result's `LaunchStatus` by its
name. The line comes at Information when the exception says the platform does not support the
operation, and at Warning for any other. A failure the launcher decides without an exception, such as an
empty URL, a relative path or no terminal emulator found, is in its `LaunchResult` alone. The line's
values are in its text, since a sink takes text, so a structured sink finds no properties in it. A sink
writes the line as a line of its own, so that its tag stays first:

```csharp
// Serilog: the line, which holds no hole, is the event's message template
var launcher = new ShellLauncher((level, line, exception) => Log.Write(ToSerilog(level), exception, line));

// An ILogger: the line is the event's state, which the formatter gives back as it is
var launcher = new ShellLauncher((level, line, exception) =>
    logger.Log(ToLogLevel(level), default, line, exception, static (state, _) => state));
```

`ToSerilog` and `ToLogLevel` stand for the app's own map from `DiagnosticLevel`'s four levels to its
logger's. Either way, the line renders as it is under `{Message:lj}`. Passed to
`logger.Log(level, exception, line)` instead, the line is a template that is not a constant, which
CA2254 flags as a suggestion.

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
| Avalonia's second report of any of these: the same exception, raised again at a later garbage collection as a task exception nobody observed | Neither logged, shown nor written on stderr by `EntryPointOptions()`, so the handler's report is the only one |
| One a handler subscribed before the package's has already handled | Left alone; its second report is neither logged, shown nor written on stderr either |

The handler is installed once, however often `InstallAvaloniaHooks()` is called.
`HandleUiThreadExceptions = false` leaves every exception to the app, and the handler then only
remembers each one, so that its second report is still known; with `ShowUnobservedTaskExceptions =
false` as well, and `FailOnUnobservedTaskExceptions` off, nothing is installed.

## Task exceptions nobody observed

A task that fails while nothing awaits it, a background save, backup or fetch, reports nothing until
a garbage collection finds it. In an Avalonia app run by `AppMain` with `EntryPointOptions()`, it is
then shown as a failure on the UI thread is, so it is not lost:

| The exception | What happens |
|---|---|
| Benign, by `IsBenignException` | Logged at Debug and kept off stderr; nothing is shown |
| Nothing but cancellations, `OperationCanceledException` or `TaskCanceledException` | Benign whatever `IsBenignException` says: abandoned work, not a failure anyone can act on. One beside a real failure is not benign |
| Any other | Written on stderr, logged at Error, and shown in a `FatalErrorDialog`, through the same one-at-a-time gate and repeat counting as a failure on the UI thread: one failure is shown once, whichever way it comes |
| Any other, with `ShowUnobservedTaskExceptions = false` | Written on stderr and logged at Warning; nothing is shown |
| Avalonia's second report of an exception the UI thread let escape | Neither logged, shown nor written on stderr |

The dialog shows the exception the task failed with, when it failed with exactly one; a task that
several failures faulted, as `Task.WhenAll` can be, is shown as its `AggregateException`. The runtime
raises the exception on its finalizer thread, and the dialog is posted to the UI thread and never
waited for. Before `InstallAvaloniaHooks()` has run, and once the app has shut down, the exception is
logged and not shown.

A run that proves a build by its exit code, a smoke run, sets `FailOnUnobservedTaskExceptions`, which
is off by default. The options count every task exception nobody observed that is not benign, from the
start of the run and whatever `ShowUnobservedTaskExceptions` says; Avalonia's second report is not
counted. When the app exits, the handler `InstallAvaloniaHooks()` subscribed to the lifetime's `Exit`
forces a full collection, so that a task that failed and that nothing references any longer is
reported then, and turns an exit code of 0 into 1 when the count is above zero, with a line at Error
giving it. A code the app set itself is kept. A task still running at exit, or still referenced, is not
caught, and the collection adds a pause at exit, as long as a full collection takes. A lifetime that
raises no `Exit`, such as a single view's, gets a Warning in the log, and the exit code is left alone.
The filter runs on the finalizer thread while the UI thread waits for that collection, so a filter
that waited on the UI thread would hang the exit.

`IsBenignException` is one filter for both paths: the handler reads it, and `EntryPointOptions()`
passes it on, so a benign task exception nobody observed is kept off stderr and logged at Debug.
Set it where logging is configured:

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

## Dialog names and ids

Every control a person can reach in the package's windows, the dialogs `AvaloniaDialogService` shows,
`FatalErrorDialog` and `NonFatalNoticeDialog`, has a name a screen reader reads, a tooltip, and an
automation id unique in its window. A UI test or a harness finds a control by its id, whatever label a
caller passed:

| Id | The control |
|---|---|
| `DialogOk` | An alert's OK, and the input dialog's |
| `DialogConfirm` | A confirmation's confirm button, whatever its label |
| `DialogCancel` | A confirmation's cancel button, the input dialog's Cancel, and the unsaved-changes prompt's |
| `DialogSave` | The unsaved-changes prompt's Save |
| `DialogDontSave` | Its Don't Save |
| `DialogInput` | The input dialog's text box |
| `DialogText` | An alert's message, and each block of a `DialogMessage`'s text |
| `DialogCode` | A `DialogMessage`'s code |
| `DialogCopyPath` | The 📋 after a path |
| `DialogOpenLink` | The ↗ after a link |
| `DialogDetails` | The fatal-error and notice dialogs' details box |
| `DialogCopy` | Their copy button |
| `DialogClose` | Their Close |

A role that repeats in one window is numbered from its second: a message with two paths shows
`DialogCopyPath` and `DialogCopyPath2`.

A button is named by its label, and the input dialog's text box by its prompt. The 📋 is named by
`TipCopyPath`, "Copy" and its path, and the ↗ by `TipOpenLink`, "Open" and its link, each of which is
its tooltip too. A block of text is named by its text, and a window by its title, so a screen reader
reads the ⚠ or ⛔ the package puts before the title of a destructive or an error dialog. A label, a path
or a link a caller passes goes into a name as given, symbol and all. Every other name and tooltip is
the package's, in the words under "Dialog strings".

Tab reaches the 📋 and the ↗, and Enter and Space press them, as UI Automation's invoke does. While one
of them has keyboard focus, Enter presses it, not the dialog's default button.

## Dialog colours

The dialogs `AvaloniaDialogService` shows take every colour from a key the package declares for Light
and for Dark, and an open dialog follows a change of theme. The first dialog that reads a key merges
the package's dictionary into the application's resources, first among its merged dictionaries, so
nothing needs adding at start-up. `FatalErrorDialog` and `NonFatalNoticeDialog` read no key.

| Key | What it paints | Light | Dark |
|---|---|---|---|
| `AppServices.ErrorBrush` | The glyph in an error's header | `#C62828` | `#F85149` |
| `AppServices.WarningBrush` | The ⚠ in a destructive action's header | `#C77700` | `#D29922` |
| `AppServices.LinkBrush` | A path, a link, and the ↗ after a link | `#1F6FEB` | `#58A6FF` |
| `AppServices.SubtleBrush` | The 📋 after a path | `#888888` | `#8B949E` |
| `AppServices.DestructiveBackgroundBrush` | A destructive button's fill | `#C62828` | `#C62828` |
| `AppServices.DestructivePointerOverBackgroundBrush` | Its fill while pointed at | `#B71C1C` | `#B71C1C` |
| `AppServices.DestructivePressedBackgroundBrush` | Its fill while pressed | `#8E1B1B` | `#8E1B1B` |
| `AppServices.DestructiveForegroundBrush` | Its label | `#FFFFFF` | `#FFFFFF` |
| `AppServices.CodeBlockBackgroundBrush` | A code block's fill | `#F4F4F4` | `#1F1F1F` |
| `AppServices.CodeBlockBorderBrush` | A code block's border | `#D0D0D0` | `#3A3A3A` |

Against a white window, and a `#1F1F1F` one in Dark, a colour that paints text reads at 4.5:1 or
more, and one that paints a glyph at 3:1 or more; a destructive button's label reads at 4.5:1 or more
on each of its fills. A theme variant that inherits neither Light nor Dark takes the Light values.

A host overrides a key by declaring it in `Application.Resources`: among its own entries, in its theme
dictionaries, or in a dictionary it merges, before or after a dialog has merged the package's.

```xml
<Application.Resources>
  <ResourceDictionary>
    <ResourceDictionary.ThemeDictionaries>
      <ResourceDictionary x:Key="Light">
        <SolidColorBrush x:Key="AppServices.CodeBlockBackgroundBrush" Color="#F6F8FA" />
      </ResourceDictionary>
      <ResourceDictionary x:Key="Dark">
        <SolidColorBrush x:Key="AppServices.CodeBlockBackgroundBrush" Color="#161B22" />
      </ResourceDictionary>
    </ResourceDictionary.ThemeDictionaries>
  </ResourceDictionary>
</Application.Resources>
```

A key declared in a style's resources, under `Application.Styles`, is found after the package's, since
an application reads its resources before its styles. Once a dialog has merged the package's
dictionary, each dictionary the host merged sits one place later among the merged dictionaries.

A destructive button keeps its fill and its white label when it is pointed at and pressed, and the 📋
and the ↗ stay unfilled: the dialog's own styles paint each button's `PART_ContentPresenter` in those
two states, the part Semi's and Fluent's buttons both name. Under a theme whose button names it
otherwise, those two states keep the theme's colours and fills. A glyph an emoji font draws in colour,
such as 📋 or ⛔, may ignore its brush.

A dialog takes the application's theme, whatever its owner's: a window given a theme of its own shows
its dialogs in the application's.

## Dialog strings

Every word the package's windows show that no caller passes comes from `DialogStrings`, in the
`Bennewitz.Ninja.AppServices.AvaloniaUI.Localization` namespace: the labels, tooltips, names and help
text of the dialogs `AvaloniaDialogService` shows and of `FatalErrorDialog` and `NonFatalNoticeDialog`,
the pickers' titles when a caller passes none, and the fatal dialogs' titles and message. A host
replaces any of them by assigning `DialogStrings.Resolver` once at start-up. The resolver is given a
key, the name of one of `DialogStrings`' properties, and returns the host's text for it, or the key
itself for a key the host does not translate, which then shows in English:

```csharp
DialogStrings.Resolver = key => key switch
{
    nameof(DialogStrings.LabelSave) => Strings.Save,
    nameof(DialogStrings.LabelDontSave) => Strings.DontSave,
    nameof(DialogStrings.TipCopyPath) => Strings.CopyPath, // "Copy {0}" in English
    _ => key,
};
```

| Key | Where it shows | Default |
|---|---|---|
| `LabelOk` | An alert's OK, and the input dialog's | OK |
| `LabelCancel` | The input dialog's and the unsaved-changes prompt's Cancel, and a confirmation's when its caller passes no label | Cancel |
| `LabelConfirm` | A confirmation's confirm button when its caller passes no label | Confirm |
| `LabelSave` | The unsaved-changes prompt's Save | Save |
| `LabelDontSave` | Its Don't Save | Don't Save |
| `LabelClose` | The fatal-error and notice dialogs' Close | Close |
| `LabelCopyToClipboard` | Their copy button, after the 📋 | Copy to Clipboard |
| `TitleSelectFolder` | The folder picker, when its caller passes no title | Select Folder |
| `TitleSelectFile` | The file picker, when its caller passes no title | Select File |
| `TitleSaveAs` | The save picker, when its caller passes no title | Save As |
| `TitleFatalError` | The fatal-error dialog, `{0}` the application's name | Application Error — {0} |
| `TitleFatalErrorNoApp` | The fatal-error dialog and the native one, before `ConfigureLogging` names the application | Application Error |
| `TitleNativeFatalError` | The native fatal-error dialog, `{0}` the application's name | {0} — Critical Error |
| `TextFatalError` | The fatal-error dialog's message, when the package shows it or a caller passes none | The application encountered an unexpected error. You can copy the details below for bug reporting. |
| `TipOk` | An alert's OK | Close this message |
| `TipAccept` | The input dialog's OK | Use the text you typed |
| `TipConfirm` | A confirmation's confirm button | Do what this dialog asks, and close it |
| `TipCancel` | A confirmation's Cancel, and the input dialog's | Close this dialog without doing anything |
| `TipSave` | The unsaved-changes prompt's Save | Save your changes, then close |
| `TipDontSave` | Its Don't Save | Close without saving your changes |
| `TipKeepOpen` | Its Cancel | Keep the window open, with your changes unsaved |
| `TipInput` | The input dialog's text box | Type here; Enter accepts it and Escape cancels |
| `TipSelectText` | A message's text | Select text here to copy it |
| `TipSelectCode` | A message's code | Select code here to copy it |
| `TipCopyPath` | The 📋 after a path, its tooltip and its name, `{0}` the path | Copy {0} |
| `TipCopied` | That 📋's tooltip once it has copied | Copied! |
| `TipOpenLink` | The ↗ after a link, its tooltip and its name, `{0}` the link | Open {0} |
| `TipCloseWindow` | The fatal-error and notice dialogs' Close | Close this window |
| `NameErrorDetails` | The fatal-error dialog's details box | Error details |
| `HelpErrorDetails` | Its help text and tooltip | Read-only exception text; Copy to Clipboard copies all of it |
| `HelpCopyErrorDetails` | The fatal-error dialog's copy button, its help text and tooltip | Copy the full error details to the clipboard |
| `NameNoticeDetails` | The notice dialog's details box | Notice details |
| `HelpNoticeDetails` | Its help text and tooltip | Read-only notice text; Copy to Clipboard copies all of it |
| `HelpCopyNotice` | The notice dialog's copy button, its help text and tooltip | Copy the full notice text to the clipboard |

`TextFatalError`'s two sentences are on two lines. Four keys carry a value as `{0}`, and the package
puts it in with the current culture: a text without `{0}` shows without the value, so two 📋 in one
window would share a name, though never an id.

A string is read each time a window is built, so a culture the host changes reaches the next dialog,
and an open one keeps the words it was built with. The resolver may be called on any thread, the one
that failed included, since the native fatal dialog's title is read there: it must return at once,
and never wait on the UI thread. A resolver that throws, or returns null or blank text, or a text
whose `{0}` cannot be filled, never fails a dialog: that string shows its default, and its first
failure is logged at Warning.

The package's glyphs, ⚠, ⛔, 📋 and ↗, stay outside the words, and a label, title or message a caller
passes is shown as given. A host's words go into names as given too, symbol and all. The windows lay
out left to right whatever the language, and the button of macOS's native fatal-error dialog reads
"OK", whatever the host's words.

Every key is public and permanent: a string the package stops showing keeps its key, and no key is
renamed, so a resolver written for one version keeps working with every later one. A key a later
version adds shows its default until the resolver maps it.

## Upgrading from 2026.4.1116

- **A code block's keys take the package's prefix.** A dialog's code block reads
  `AppServices.CodeBlockBackgroundBrush` and `AppServices.CodeBlockBorderBrush`, and no longer
  `AppCodeBlockBackgroundBrush` or `AppCodeBlockBorderBrush`. An app that declared its own values
  under the old keys declares them under the new ones; until it does, its code blocks take the
  package's, `#F4F4F4` and `#D0D0D0` in Light and `#1F1F1F` and `#3A3A3A` in Dark. An app that declared
  those same values can drop them, and one that declared neither key sees its code blocks filled and
  framed, where they were not before.
- **The dialogs' colours follow a dark theme.** A path, a link, the 📋 and the glyph in an error's or a
  destructive action's header take lighter colours in Dark, from the keys under "Dialog colours", and an
  open dialog follows a change of theme. In Light they look as before.
- **The 📋 after a path and the ↗ after a link are buttons.** Tab reaches each, Enter and Space press
  it, and a screen reader names it by what a press does. Each looks as before, but for the theme's focus
  ring when it has keyboard focus.
- **Every control in the dialogs has a name, a tooltip and an automation id.** A UI test finds each by
  its id, under "Dialog names and ids", whatever label a caller passed.
- **`IDialogService.ShowConfirmAsync`'s labels default to null.** Both overloads take
  `string? confirmLabel = null` and `string? cancelLabel = null`, where they took "Confirm" and "Cancel".
  Null shows the host's words, `DialogStrings.LabelConfirm` and `DialogStrings.LabelCancel`, and a label
  given is shown as given. A class that implements `IDialogService`, a test double among them, changes
  those two parameters to match. A call compiled against an earlier version still passes "Confirm" and
  "Cancel", which show in English until it is rebuilt.
- **`FatalErrorDialog.DefaultMessage` is gone.** Pass `DialogStrings.TextFatalError`, which holds the same
  English and the host's words once the host replaces them, or a message of your own.
  `AvaloniaDiagnostics.ShowFatalErrorDialog` shows it for a null message, as before.
- **Every word the dialogs show can be the host's.** `DialogStrings.Resolver` replaces each, under
  "Dialog strings"; a host that sets none sees the dialogs as before.
- **The packages' own log lines take the family's form**, an `[Area.Kind]` tag and then camelCase pairs,
  where each was a sentence. A host that searched its log for a line's old text searches for its tag, such
  as `[UiThread.Exception]` or `[Failure.Repeat]`, and one that read a line's properties by name reads the
  new names: `Count` for `UnobservedCount` and `RepeatCount`; `Type`, `Message` and `Site` for
  `ExceptionType`, `ExceptionMessage` and `ThrowSite`; `Key` and `Reason` for `DialogStringKey` and
  `Failure`. The fatal line no longer names the app. Avalonia's messages read
  `[Avalonia.Log] area=… text="…"`, a binding's failed conversion without the value it could not
  convert; the binding validation line carries the exception's type and a stack, never its message, and
  a text box's text as its length. A `DiagnosticSink` passed to `ShellLauncher` is handed
  `[Shell.Launch] action=… reason=…`, and the Trace sink renders each message as the rolling file does.
- **Each run can log its start and its end.** `AppMainOptions.WriteLogLine`, new, writes
  `[App.Start] app=… version=… pid=…` just before the work and one `[App.Exit]` line as the run ends,
  before `FlushLog` (see "Log lines"). It is null by default, so an app that sets nothing logs nothing
  new. `EntryPointOptions()` sets it, so an Avalonia app that calls `ConfigureLogging` gains both lines
  with no change of its own: the start line first in the log `ConfigureLogging` makes, and the exit line
  after the repeat counts still pending. One that sets Serilog up itself, without `ConfigureLogging`,
  gains the exit line alone until it passes `Log.Information` as the writer. An app that wrapped
  `FlushLog` to log its own exit can drop that line, and a test that asserts over every event of such a
  run leaves the two out by their tags.
- **`LogRedaction` keeps a secret out of a log line**, new in `Bennewitz.Ninja.AppServices.Abstractions`:
  `LogRedaction.Redact(key, value)` gives `***` for a value under a key that may name a secret, under
  "Log lines".

## Upgrading from 2026.4.1113

- **A smoke run can fail on a task exception nobody observed.** `FailOnUnobservedTaskExceptions`, off
  by default, makes a run that would exit with 0 exit with 1 when one that is not benign was reported
  during it, after a full collection at exit has looked for those not yet reported (see "Task
  exceptions nobody observed"). Nothing changes for an app that leaves it off. A smoke run sets all
  three switches from its flag:

  ```csharp
  HandleUiThreadExceptions = !smoke,
  ShowUnobservedTaskExceptions = !smoke,
  FailOnUnobservedTaskExceptions = smoke,
  ```

## Upgrading from 2026.4.1112

- **A task exception nobody observed is shown, by default.** One that is not benign is logged at
  Error, where `2026.4.1112` logged it at Warning, and shown in a `FatalErrorDialog`.
  `ShowUnobservedTaskExceptions = false` keeps `2026.4.1112`'s Warning and no dialog. A smoke run that
  proves a build by its exit code sets both switches from its flag:

  ```csharp
  HandleUiThreadExceptions = !smoke,
  ShowUnobservedTaskExceptions = !smoke,
  ```

  The first lets a UI-thread failure end the run. The second keeps the run from opening a dialog,
  which never blocks the UI thread but, like any window, holds open an app that ends when its last
  window closes. An unobserved exception leaves the exit code alone either way, and reaches stderr
  unless it is benign; the release after `2026.4.1113` adds a third switch,
  `FailOnUnobservedTaskExceptions`, so that a smoke run fails on one.

- **An app that shows a dialog of its own for these exceptions**, from a callback that wraps
  `OnUnobservedTaskException`, drops it or sets `ShowUnobservedTaskExceptions = false`; otherwise one
  failure shows two dialogs.
- **An unobserved exception that is nothing but cancellations is benign**: logged at Debug and kept off
  stderr, where `2026.4.1112` logged it at Warning and wrote it on stderr. One beside a real failure is
  not benign, and a cancellation that escapes on the UI thread is handled as before.
- **The second report of a UI-thread exception an app's own handler dealt with is no longer logged.**
  `2026.4.1112` knew the second report only of what the package's handler handled, so an app on the
  second or third way out below had it logged at Warning and written on stderr. The handler is now
  subscribed while either switch is on, and remembers every exception the dispatcher reports.
- A repeat's count line begins "An exception repeated", where it began "A UI-thread exception
  repeated", since a failure may now come either way.

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
