# 00009 — Log lines in the family's form

> Status: **approved 2026-11-24**. Supersedes nothing. Bennewitz.Ninja.Templates' `plans/00014`,
> approved by the maintainer on 2026-11-23, sets one form for every log line in the family, and by its
> decisions 5, 6 and 8 asks this repository for a plan of its own. The maintainer's choice of the same
> day: draft it now, with no setup call for apps without Avalonia.

Every app the templates generate starts in `AppServices.EntryPoint`, which writes no log line, so no log
says when a run began or how it ended. `plans/00014` gives every line the family writes one form, an
`[Area.Kind]` tag and then camelCase pairs, which an agent greps by its tag and reads without parsing
prose, and a new CodeQuality rule will hold it in every repository that pins CodeQuality, this one
among them. This plan has the entry point write a start line and an exit line for every run, through a
callback, so that it still references nothing. It adds the one redaction helper `plans/00014` asks for,
in the package that references nothing. And it brings the packages' own lines, 24 templates in 9
files and the one sentence the base package hands a diagnostic sink, into the form before that rule
reaches this repository's build.

## The requirement

| # | From the request |
|---|---|
| a | Every run writes `[App.Start] app=… version=… pid=…` once the app's logging is set up, and `[App.Exit] code=… elapsedMs=…` before its log is flushed and before a host disposes it, from `AppServices.EntryPoint`, through a new `AppMainOptions` callback beside `FlushLog`, so the entry point keeps referencing nothing and every app the templates generate has both without writing them (`plans/00014`, decision 5) |
| b | A value under a sensitive key goes through one helper in `AppServices.Abstractions`, modelled on ClaudeForge's `SensitiveKeys`, which writes `***` (decision 6; Templates confirmed `***` on 2026-11-23) |
| c | AppKit's own lines take the form before the CodeQuality rule reaches this repository's build: an `[Area.Kind]` tag, camelCase key=value pairs only, free text quoted by hand as `name="{Name}"`, an enum passed as its name, no `:l`, and every sink rendering `{Message:lj}` (decisions 1 to 3 and 8) |
| d | No setup call for apps without Avalonia, for now: Templates' `bbconsole` wires `BucketedRollingFileSink` itself. The maintainer's choice of 2026-11-23 |

## What exists

Read on 2026-11-23, at `0a861b2`.

| Source | What it shows |
|---|---|
| `AppMain.cs`, `EntryRun.cs` | Three kinds of run. Each hooks the process-wide exception events and process exit before anything else, answers `--version` before the work, and flushes the app's log once through `FlushLog`: in a `finally` when the work ends, on SIGTERM in a console or desktop run (`RegisterSigterm`, 280 to 293), on an exit no `finally` sees (`OnProcessExit`, 275), and when the runtime is ending the process on another thread's exception (`OnUnhandledException`, 235 to 247). Its reports go to stderr, `fatal: …` and `warning: …`, and it writes no log line. Each kind decides its exit code after the `finally` that flushes, or in the `catch` before it (`AppMain.cs`, 49 to 68) |
| `AppMainOptions.cs` | A sealed record of optional callbacks; one that throws is reported on stderr and changes nothing else (15 and 16). It stays binary compatible with `2026.4.1110` (the root `AGENTS.md`) |
| Templates' `bbavalonia`, `Program.cs` | Configures logging inside the run, after the entry point has hooked the exception events, "so a failure configuring it is still reported" (21 to 27). Until then Serilog's static logger drops every event |
| Templates' `bbweb` and `bbapi`, `Program.cs` | Build their host inside the work and dispose it there, `await using var app` (38), so the work returns after the host and the loggers it owns are gone. They log through Microsoft.Extensions.Logging's console today; `plans/00014`, decision 2a, moves them to Serilog through Serilog.Extensions.Hosting |
| `AvaloniaDiagnostics.cs` | `EntryPointOptions()` is built in `Main`, before `ConfigureLogging`. Its `OnFatal` writes `Log.Fatal` (459), and its `FlushLog` writes the repeat counts still pending before `Log.CloseAndFlush` (463 to 473). `ConfigureLogging` runs once per process (155 to 158) and sets `Log.Logger` (199), writing to a `BucketedRollingFileSink` and, with `EnableTraceSink`, to Serilog's Trace sink (192). `AvaloniaDiagnosticsOptions.AppName` is the name the crash dialogs show, not the assembly's |
| The packages' own lines | 24 templates in 9 files, none in the form, each under "The lines" below. Three carry text the package does not write: Avalonia's messages (`SerilogAvaloniaSink.cs`, 91 and 111 to 115), the host's own event lines (`EnqueueEvent`, 292) and the rolling file's rendered lines (`BucketedRollingFileSink.cs`, 196). `UiThreadExceptions` passes eight templates through one guarded `Write` (440 to 450), so its call to Serilog shows a variable, not a template. Two templates carry `:l` holes (`AvaloniaDiagnostics.cs` 536, `UiThreadExceptions.cs` 437) |
| `BucketedRollingFileSink.cs` | Renders `{Message:lj}` (48); its inner file logger writes the rendered line as `{Message:lj}{NewLine}` (281) |
| `BindingValidationErrorLogger.cs` | Its context holds up to 30 characters of a text box's text (225, 239 to 250). Its stack is the exception's own text when the exception was thrown, and the stack at the handler only when it was built and never thrown (102 to 104). Avalonia 12.1.3 builds the exception for a value it cannot convert with that value, whole, in its message: `Could not convert '{valueString}' ({valueTypeName}) to {type}.` (`BindingExpression.cs`, 339 to 342; `UntypedBindingExpressionBase.cs`, 311 to 313 and 353 to 355) |
| `tests/AppServices.Tests` | Three places pin a line's text: `FailOnUnobservedTaskExceptionsTests` (287) and `UiThreadExceptionHandlerTests` (864) by its template, and `UiThreadExceptionHandlerTests` (259 and 260) through `RenderMessage()`. Five read a line's properties by name: `Lifetime` and `UnobservedCount` (`FailOnUnobservedTaskExceptionsTests`, 291 and 485), `RepeatCount` (`UiThreadExceptionHandlerTests`, 867), and `Failure` and `DialogStringKey` (`DialogStringsTests`, 462 and 674). One asserts that an `AppMain` run logs nothing it does not expect (`UiThreadExceptionHandlerTests`, 484 to 491). No test reaches the binding validation logger's two lines, nor the three clipboard failures |
| ClaudeForge, `SensitiveKeys.cs` | `IsSensitive(key)`: a key is sensitive when any of its dot-separated segments is `env`, `headers`, `credentials`, `auth` or `authorization`, or when it contains `token`, `secret`, `password`, `apikey`, `api_key`, `api-key`, `bearer`, `private`, `accesskey`, `access_key` or `access-key`, ignoring case either way; never for merely containing "key". Its marker is `[redacted]`. ClaudeForge's `EnvironmentEditorViewModel.cs` imports both its namespace and `Bennewitz.Ninja.AppServices.Abstractions` (14 and 15), and names `SensitiveKeys` bare (375) |
| `AppServices.Abstractions` | References nothing (`LayeringTests`); its types sit in `Bennewitz.Ninja.AppServices.Abstractions`, beside `DiagnosticSink` |
| `ShellLauncher.cs`, `DefaultShareService.cs`, in `AppServices` | `ShellLauncher` hands the `DiagnosticSink` a consumer passes it one sentence, `{what} ended as {status}` (247 to 250), `what` one of four operations (88, 121, 170 and 207); a sink takes "one line, already formatted" (`DiagnosticSink.cs`, 27), and ClaudeForge passes none. `DefaultShareService` writes four messages to `System.Diagnostics.Trace` through one helper, as `[DefaultShareService] {message}` (267, 300, 347 and 361; 320 to 322) |
| CodeQuality | Inspections plans the rule, whose id it has not given. This repository takes CodeQuality through Templates' syncs, at `2026.4.1113` today |

## Decisions

| # | Decision | Why |
|---|---|---|
| 1 | **`AppMainOptions.WriteLogLine`, a `LogLineWriter?`, writes the entry point's two lines to the app's log**, at Information. `LogLineWriter` is a public delegate in `AppServices.EntryPoint`, `void LogLineWriter(string messageTemplate, params object?[] propertyValues)`, by the maintainer's choice of 2026-11-24: a message template in the family's form, its named holes filled from the values in order, as Serilog's and Microsoft.Extensions.Logging's methods fill them, and never a format string, whose numbered holes `{0}` the named ones would make throw. A Serilog app passes the method group `Log.Information`, and an app with an `ILogger` the method group `logger.LogInformation`. The templates are the entry point's own, held by its tests. A callback that throws is reported on stderr, as any other is | `plans/00014` asks for one member beside `FlushLog`, and the entry point reaches the log only through callbacks, since it references nothing. A template with its values keeps `app`, `code` and `elapsedMs` as properties a structured sink can query, where a rendered string would not. A named delegate states that contract where a caller reads it, takes its values one by one as Serilog's methods do, and still binds both method groups, as the family's `DiagnosticSink` is a named delegate too. A method group is no call, so neither CA2254 nor the coming rule sees an app pass its logger a template that is not a constant, as a lambda would show them. A new `init` member keeps the record binary compatible |
| 2 | **The start line, `[App.Start] app={App} version={Version} pid={Pid}`, is offered once per run, just before the work runs.** `app` is the program assembly's name, as the stderr reports name it; `version` its `PublicVersion`, as `--version` prints it, or `unknown` when it carries none; `pid` the process's id. A run that answers `--version` writes neither line | The work is the first point every run passes after its hooks are in place. `--version` prints and exits before any log exists |
| 3 | **A callback whose log is not set up yet holds the start line, and writes it once the log is.** `AvaloniaDiagnostics.EntryPointOptions()`' callback writes at once when `ConfigureLogging` has run, which it does once per process; until then it holds the line, which `ConfigureLogging` writes as the configured log's first line, and drops it if its run's exit line comes first. `ResetForTests` drops what is held. The same callback writes the repeat counts still pending before it writes an `[App.Exit]` line, so the exit line follows every count pending then. An app that sets its logger up before calling `AppMain`, as Serilog's own guidance does, passes `Log.Information` as it is. Templates' `bbconsole`, `bbweb` and `bbapi` do so, by the maintainer's choice of 2026-11-23, so a failure setting up their log is reported by the runtime rather than by `AppMain` | `bbavalonia` configures its log inside the run on purpose, so that a failure configuring it is reported, and no entry point can know when an app's log is ready. The one callback that knows holds the line: the entry point keeps one member, an Avalonia app writes nothing new, and nor do the templates' other three. A held line would otherwise outlive its run, as a test host's many runs would show. `EntryPointOptions()`' `FlushLog` writes the repeat counts, and the entry point flushes after the exit line, so without the callback's help they would follow it; the `[App.Exit]` tag is the family's contract, so the callback may read it |
| 4 | **The exit line is written once in every run that offered a start line, once the run's exit code is known and before `FlushLog`:** `[App.Exit] code={Code} elapsedMs={ElapsedMs}` when the work returns or throws, with 0 or its own code, 1, 2 or 130; the same with `Environment.ExitCode` on an exit no `finally` sees; `[App.Exit] signal=SIGTERM elapsedMs={ElapsedMs}` on SIGTERM in a console or desktop run, which the signal then ends; `[App.Exit] reason=unhandledException elapsedMs={ElapsedMs}` when the runtime is ending the process on another thread's exception; and `[App.Exit] reason=hostAborted elapsedMs={ElapsedMs}` when a design-time tool stops the host with a `HostAbortedException`, which passes through untouched. `elapsedMs` is a whole number of milliseconds since the run started. Each kind decides its code before it flushes | A log with a start line and no exit line reads as a death, so every run that wrote the one writes the other. The code is known only once the work has ended. Neither a signal, the runtime's own exit nor a design-time tool's stop hands the entry point a code, so those lines say what ended the run instead. `--version` flushes too, but offers no start line, so it writes no exit line |
| 5 | **A web app's log is one its host does not own**, by the maintainer's choice of 2026-11-23. The exit line is written in `AppMain`'s own `finally` around the work, once the code is known, and a web app's work has disposed its host by then. Serilog's static logger outlives a host that `AddSerilog()` or `UseSerilog()` wired by default, which leaves it undisposed, and `FlushLog = Log.CloseAndFlush` flushes it after the exit line. `docs/appservices.md` says so, and step 2 measures it with Serilog.Extensions.Hosting, in scratch | `plans/00014` asks for the exit line "before a host disposes it", which assumes the host owns the log. Code around the work, `AppMain`'s or code wrapping `Main`, runs only once the work has returned, and only then is the code known. A logger the host disposes drops every later write; one it does not own takes the line. Templates was told on 2026-11-23, before its step 2 wires `bbweb` and `bbapi` |
| 6 | **`LogRedaction`, a public static class in `AppServices.Abstractions`**, in its root namespace: `Redacted`, the constant `***`; `IsSensitive(string key)`, ClaudeForge's two passes and both its lists, unchanged; and `Redact(string key, string? value)`, `Redacted` for a sensitive key and the value as it is otherwise, for a line such as `[Settings.Change] key={Key} value="{Value}"`. Tests hold every entry of both lists and ClaudeForge's documented examples, both ways | `plans/00014` asks for one helper with one list, in a package that references nothing, so a library that logs only through `ILogger` can call it. ClaudeForge's lists are what its audit arrived at. The name is not ClaudeForge's: its `EnvironmentEditorViewModel.cs` imports both its own `SensitiveKeys`' namespace and this package's, so a second `SensitiveKeys` would make the name ambiguous there, CS0104, the day it upgrades |
| 7 | **The packages' own lines take the form**, each as "The lines" below gives it: an `[Area.Kind]` tag, camelCase pairs whose holes are their keys in PascalCase, free text quoted by hand, an enum passed as its name, no `:l`, an exception in the call's exception argument, and every Warning's, Error's and Fatal's cause in `reason=`. A value the package does not have is `unknown`. Each line keeps its level, and its exception but for the binding validation line, which drops it, since Avalonia writes the value it could not convert into its message. Every sink the package configures renders `{Message:lj}`: the rolling files do, and the Trace sink is given an output template that does. `ShellLauncher`'s sentence to a `DiagnosticSink` takes the form too, its values in its text, since a sink takes text; `DefaultShareService`'s messages to `Trace` do not, since they never reach an app's log. Both by the maintainer's choice of 2026-11-24. The tests that read the old text or the old properties read the new, never through `RenderMessage()`, and one helper in the tests, `LogLineForm`, holds to the form the template of every line a test reaches | `plans/00014`'s decisions 2 and 3, as written: `RenderMessage()` quotes strings itself, so a test reading through it reads a line no sink writes. The rule will read the packages' constant templates once its release reaches this repository, but not a template it cannot see, as `UiThreadExceptions`' eight behind its guarded `Write`. So the tests hold what they reach, and step 2 lists what they do not |
| 8 | **Text the package forwards is a value; text it only carries stays as written.** Avalonia's message is rendered with its own values into one string, each value as its own text, an enum by its name and the rest in the invariant culture, and written as `[Avalonia.Log] area={Area} text="{Text}"`. That is the line `plans/00014`'s decision 1 gives but for its hole, which there is `{Message}` and here is named for its key, as every other line's is. The host's event lines (`EnqueueEvent`) and the rolling file's rendered lines are written as they came, each the whole of a line in its file, and are suppressed at those two sites, with the rule's id and this reason, in the change that takes the rule's release | Avalonia's text reaches the app's log among the package's own lines, so it takes their form. Serilog's own rendering quotes a string, which `plans/00014`'s decision 3 bars, and `{Message:lj}` quotes an enum, a date and a guid, which would nest quotes inside `text="…"`, so the package renders Avalonia's values itself. The events file is the host's own record, whose lines are the host's to form, and the rolling file's inner logger writes a line already rendered: a tag there would double every line |
| 9 | **AppServices' next release carries it, on the maintainer's word**, with `plans/00007` and `plans/00008`, which are on `main` before it. Nothing in it breaks a consumer's build: `WriteLogLine` and `LogRedaction` are new. A host that searched its log for the packages' old text, or read a line's properties by their old names, finds the new ones instead, which "Upgrading from 2026.4.1116" says, and the change that renames them is marked `!`. Templates is told the API once this plan is approved, and the version on release | A release packs `main`, so the next one carries all three; when it comes is the maintainer's. A renamed property breaks a query that reads it by name, which the root `AGENTS.md` marks `!`. Templates raises its pins once this and the rule are out (`plans/00014`, step 5) |

### The lines

| File, line | Level | Today | In the form |
|---|---|---|---|
| `AvaloniaDiagnostics.cs` 292 | Information | `{EventLine}`, the host's line | Unchanged (decision 8) |
| the same, 459 | Fatal | `{AppName} stopped on an unhandled exception` | `[App.Fatal] reason=unhandledException` |
| the same, 499 | Debug | `A benign task exception nobody observed` | `[Task.Unobserved] benign=true` |
| the same, 513 | Warning | `A task exception nobody observed` | `[Task.Unobserved] reason=notObserved benign=false` |
| the same, 516 | Warning | `The IsBenignException filter threw on the task exception above, …` | `[Task.BenignFilter] reason=threw` |
| the same, 535 | Warning | `FailOnUnobservedTaskExceptions is set, but the app's lifetime, {Lifetime:l}, raises no Exit, …` | `[App.Smoke] reason=noExitEvent lifetime={Lifetime}` |
| the same, 594 | Error | `Task exceptions nobody observed were reported during the run, {UnobservedCount} of them, …` | `[App.Smoke] reason=unobservedTaskExceptions count={Count} code=1` |
| `BindingValidationErrorLogger.cs` 106 | Warning | `[BindingValidation] {Control} (DataContext={DataContext}){NewLine}  {ContextDetails}…` | `[Binding.Validation] reason=exception control="{Control}" dataContext="{DataContext}" context="{Context}" type="{Type}" stack="{Stack}"`, with no exception: Avalonia writes the value it could not convert into the exception's message, so the line carries the exception's type and a stack, its own when it was thrown and the one at the handler when it was not, last since it spans lines; a text box's text given in the context as its length |
| the same, 120 | Warning | the same, ending in `{Error}` | `[Binding.Validation] reason=error control="{Control}" dataContext="{DataContext}" context="{Context}" error="{Error}"`, the context as above |
| `SerilogAvaloniaSink.cs` 91 | Avalonia's | `[Avalonia:{Area}] {Message}` | `[Avalonia.Log] area={Area} text="{Text}"` |
| the same, 111 to 115 | Avalonia's | `[Avalonia:{Area}] ` before Avalonia's own template | The same, Avalonia's template rendered with its values |
| `DialogMessageContent.cs` 301 | Warning | `Copying {Path} to the clipboard failed` | `[Dialog.Copy] reason=clipboardFailed path="{Path}"` |
| `FatalErrorDialog.cs` 163 | Warning | `Copying the error details to the clipboard failed` | `[Dialog.Copy] reason=clipboardFailed what=errorDetails` |
| `NonFatalNoticeDialog.cs` 149 | Warning | `Copying the notice text to the clipboard failed` | `[Dialog.Copy] reason=clipboardFailed what=notice` |
| `DialogStrings.cs` 292 | Warning | `DialogStrings.Resolver {Failure} for {DialogStringKey}, so its default is shown` | `[Dialog.Strings] key={Key} reason={Reason}`: `threw`, `gaveNull`, `gaveBlank` or `badPlaceholder` |
| `UiThreadExceptions.cs` 169 | Error | `A task exception nobody observed` | `[Task.Unobserved] reason=notObserved benign=false`, at Error where 513 writes Warning, since this path may show a dialog |
| the same, 172 | Warning | `The IsBenignException filter threw on the task exception above, …` | `[Task.BenignFilter] reason=threw` |
| the same, 189 | Error | `The dialog for the task exception above could not be shown` | `[Dialog.Fatal] reason=cannotPost` |
| the same, 309 | Debug | `A benign exception on the UI thread, ignored` | `[UiThread.Exception] benign=true` |
| the same, 339 | Error | `An exception on the UI thread that nothing handled; the app keeps running` | `[UiThread.Exception] reason=unhandled benign=false` |
| the same, 342 | Warning | `The IsBenignException filter threw on the exception above, …` | `[UiThread.BenignFilter] reason=threw` |
| the same, 401 | Error | `The dialog for the exception above could not be shown` | `[Dialog.Fatal] reason=cannotShow` |
| the same, 436 | Error | `An exception repeated {RepeatCount} more times: {ExceptionType:l}: {ExceptionMessage:l}, thrown {ThrowSite:l}` | `[Failure.Repeat] reason=repeated count={Count} type="{Type}" message="{Message}" site="{Site}"`: not the UI thread's area, since a repeat may come either way (`docs/appservices.md`, "Upgrading from 2026.4.1112") |
| `BucketedRollingFileSink.cs` 196 | Information | `{RawLine}`, a rendered line | Unchanged (decision 8) |
| `ShellLauncher.cs` 249 | Information for `Unsupported`, else Warning, to the `DiagnosticSink` | `{what} ended as {status}` | Text such as `[Shell.Launch] action=terminal reason=NotFound`, whole, no template: `action` one of `terminal`, `reveal`, `editor` and `url`, `reason` the `LaunchStatus`' name, the exception as before |

### Dismissed

- **A setup call for apps without Avalonia**, a `ConfigureLogging` and `EntryPointOptions()` pair in
  `AppServices.Logging`. The maintainer chose not now, on 2026-11-23: the callback is wired in one line
  and the sink in a few, and the pair would make `AppServices.Logging` reference `AppServices.EntryPoint`.
- **Two callbacks, one for each line, handed the facts.** Each app would write the text, and the form
  would be every app's to keep.
- **The start line written by `ConfigureLogging` alone.** Only Avalonia apps would have one.
- **A public `AppMain` method an app calls once its log is set up**, which writes the start line then.
  It would place the line exactly in every app, but each app whose log is set up inside its run must
  remember the call, and one that forgets has no start line and no error. Holding asks nothing of an app
  whose log is set up before `AppMain`, nor of an Avalonia app. The maintainer chose holding, and the
  templates setting their log up first, on 2026-11-23.
- **The exit line written before the host is disposed.** The code is not known until the work has
  returned, and the host is the work's.
- **A public `AppMain` method a web app calls in its own `finally`, before its host is disposed**, for a
  host that owns its log. More API, and the code is not final there, since an exception may still be on
  its way out. The maintainer chose a log the host does not own, on 2026-11-23.
- **A rendered string in place of a template and its values.** It would lose the properties a structured
  sink queries, and an app writing the text as a template would trip CA2254 and the coming rule.
- **`Action<string, object?[]>` for the writer.** It takes its values as an array, and only a doc
  comment would say the string is a message template; the maintainer chose the named delegate on
  2026-11-24.
- **`LoggerMessage` or Microsoft.Extensions.Logging in the entry point.** It references nothing, and a
  native AOT app takes it whole.
- **ClaudeForge's `[redacted]`.** `plans/00014` says `***`, which Templates confirmed on 2026-11-23.
- **Tagging the host's event lines and the rolling file's rendered lines.** Each line is already formed
  by its writer.
- **Moving `UiThreadExceptions`' eight templates out of its guarded `Write`**, so that the rule reads
  them. Every one of those writes needs the guard, and the tests hold the eight instead.

## Limits

- An app whose log is set up inside its run, and whose callback writes at once, loses its start line.
- A web app whose host disposes its log loses its exit line.
- SIGTERM's exit line and a crash's carry no code: the signal and the runtime end the process after the
  handler returns.
- A run that ends with no `finally` running logs `Environment.ExitCode`, which only `Environment.Exit` is
  sure to have set.
- An app that sets its log up in `Main`, before `AppMain`, and fails doing so, exits with the runtime's
  code and report, not `AppMain`'s 1.
- The stderr reports, `fatal: …` and `warning: …`, are not log lines, and keep their text.
- Avalonia's own properties are kept only in the text.
- A quote inside a quoted value is not escaped (`plans/00014`, decision 3).
- The rule's release reaches this repository through a Templates sync, on the maintainer's word; until
  then the tests are the guard, and review is the guard of a line no test reaches.
- A line from Avalonia keeps Avalonia's level, and its text is its only reason.
- `ShellLauncher`'s sentence carries its values in its text, since a `DiagnosticSink` takes text: a
  structured sink finds no properties in it.
- The binding line's `error` is the app's own validation message, logged as the app gives it.
- A writer that formats its string, as `Console.Out.WriteLine(format, values)` does, binds to
  `LogLineWriter` too, and throws on the named holes; the throw is reported on stderr.

## Scope

**In:**
- decisions 1 to 9 and their tests;
- the doc comments;
- `docs/appservices.md`: "Log lines", saying what the entry point writes, `LogLineWriter` and how an app
  wires it for Serilog and for an `ILogger`, that a web app's log outlives its host, `LogRedaction`, the
  packages' own lines, and how a `DiagnosticSink` writes `ShellLauncher`'s line as a line of its own;
  and "Upgrading from 2026.4.1116";
- `src/AGENTS.md`: `LogRedaction` in the Abstractions row and `LogLineWriter` in the EntryPoint row, and
  rows for the two lines and for the form of the packages' lines;
- the root `AGENTS.md`: an AppServices invariant on the two lines;
- `tests/AGENTS.md`: `LogLineForm` and its rule;
- `PROGRESS.md`;
- the release, with `plans/00007` and `plans/00008`;
- telling Templates: the API on approval, the version on release.

**Out:**
- the CodeQuality rule, which Inspections plans, and the two suppressions decision 8 names, made in the
  change that takes its release;
- Templates' move to Serilog, and its pins;
- a setup call for apps without Avalonia;
- ClaudeForge's `SensitiveKeys` and its lines, its own to change;
- ScopedEditors, which writes no line;
- `DefaultShareService`'s four messages to `Trace`, which reach a debugger and never an app's log;
- escaping a quote inside a value.

## Steps

| # | Step | Verified by |
|---|---|---|
| 1 | This plan, committed on its own once approved, and its API told to Templates | CI green; told |
| 2 | **Measure first.** Through Serilog with a `{Message:lj}` sink, and through Serilog.Extensions.Logging's `ILogger`: what the start and exit lines write, each value bare or quoted as the template has it, an `int` and a `long` as numbers; and that both method groups bind to `LogLineWriter`, Serilog's to its non-generic `params` overload, and build without a warning under the analysers the templates run, CA2254 among them. In scratch, a web app run by `RunHostAsync` with Serilog.Extensions.Hosting's `AddSerilog()`: that a line written after the host is disposed reaches the static logger's sink, and with `dispose: true` that it does not. Avalonia's template with its values: the text the package's own rendering gives, each value as its own text. A `DiagnosticSink` that writes `ShellLauncher`'s line as its own, through Serilog's `Log.Write` with the line as its template and through `ILogger.Log` with a formatter: the line each writes, and no CA2254. Which of the 25 lines a test reaches, and which tests assert over every event of a run | Each answer recorded in `PROGRESS.md`. A decision a measurement refutes goes back to the maintainer before step 3 |
| 3 | **The entry point**: `WriteLogLine`, the start line and the exit line on every path | `AppMainTests`: each line's template and values in each kind; the order start, work, exit, flush; one exit line however many paths reach the flush; none for `--version`; a throwing callback reported and nothing else changed. `EntryPointProbeTests`, in a child process: SIGTERM's exit line before the flush, the native AOT binary the same. `EntryPointWebProbeTests`: the exit line once the work has returned, before `FlushLog`, and `reason=hostAborted` on a stop at `HostBuilt`. Each planted break seen red: no start line, the exit line after the flush, twice, missing on SIGTERM or on a design-time stop, `elapsedMs` from the wrong start, a line for `--version`. The native AOT publish has no warning, and package validation against `2026.4.1116` reports nothing new |
| 4 | **The packages' own lines**: `EntryPointOptions()`' held lines, and every line in "The lines" | The held start line written first, once, after `ConfigureLogging`; nothing written when it never runs, and nothing carried into a later run; the exit line after every repeat count pending then. Each line a test reaches: its template, level and exception, through `LogLineForm`; a line behind `Write` that no test reaches gets one. The eight places that read the old text or properties read the new, none through `RenderMessage()`, and each test that asserts over every event of a run leaves the entry point's two lines out by their tags. One Avalonia message's text pinned exactly; `ShellLauncher`'s sentence for each of its four operations, through a sink a test passes; the Trace sink rendering `{Message:lj}`; a value the binding could not convert never in the binding line's rendered event, its message and exception both read, and a text box's text there as its length. Each planted break seen red: a line back to its old text, a held line dropped, written twice or written into a later run, a repeat count after the exit line |
| 5 | **`LogRedaction`** | Every entry of both lists, ClaudeForge's examples, and its negatives `uniqueKey`, `locKey` and `headerSize`, each way; case ignored; `Redact` both ways. `LayeringTests` still pass. Each planted break seen red: an entry dropped, case honoured, the segment pass gone |
| 6 | `docs/appservices.md`, the `AGENTS.md` rows and `PROGRESS.md` | `repo-conventions check` conforms. CI green |
| 7 | Release with `plans/00007` and `plans/00008`, as their steps 7 and 4, on the maintainer's word | As those steps |
| 8 | The day it is published, tell Templates the version and the API | Told |
