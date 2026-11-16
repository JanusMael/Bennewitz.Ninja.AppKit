# 00004 — A smoke run fails on a task exception nobody observed

> Status: **approved 2026-11-16**. Supersedes nothing. The maintainer's choice of 2026-11-13,
> made through `choices` in the Templates session, which relayed it (Templates' `PROGRESS.md`, in
> JanusMael/Bennewitz.Ninja.Templates#98).

A smoke run proves a build by its exit code. A task exception nobody observed never changes that code:
`AppMain` writes it on stderr and the package logs it, and only if a garbage collection finds the
faulted task before the run ends, which in a run of a few seconds it rarely does. So `bbavalonia`'s
smoke check passes over a background failure. The maintainer chose to have the package catch it, over
leaving it and over each template forcing the collection itself.

## The requirement

| # | From the request |
|---|---|
| a | An option in `AvaloniaDiagnostics`, for a smoke run |
| b | Before such a run exits, a full collection: `GC.Collect`, `GC.WaitForPendingFinalizers`, `GC.Collect` |
| c | If a task exception nobody observed that is not benign was reported, the run exits non-zero |
| d | Every Avalonia app has it, and `bbavalonia` takes it once released |
| e | The option's shape and name are AppServices'. A task still running at exit cannot be caught |

## What exists

| Source | What it shows |
|---|---|
| `AppMain.RunDesktop` | Returns the work's own exit code, unless Ctrl+C ended the run (`EntryRun.Completed`), and 1 when an exception escapes the work |
| `bbavalonia` | Its work returns `StartWithClassicDesktopLifetime(args)`. A smoke run ends with `desktop.Shutdown(0)`, posted from `App`'s `Opened` handler, and sets `HandleUiThreadExceptions` and `ShowUnobservedTaskExceptions` to `!App.Smoke` |
| Avalonia 12.1.3, `ClassicDesktopStyleApplicationLifetime.DoShutdown` | Raises `Exit` with a `ControlledApplicationLifetimeExitEventArgs` carrying the exit code, takes the args' `ApplicationExitCode` back as the code, and only then ends the main loop and shuts the dispatcher down (read in its source at the tag). `ApplicationExitCode` has a public setter. `IControlledApplicationLifetime` declares `Exit`, and is `[NotClientImplementable]` |
| `EntryRun.OnUnobservedTaskException` | Writes a task exception nobody observed that is not benign on stderr, then calls the callback |
| `AvaloniaDiagnostics.EntryPointOptions()`' callback | Skips Avalonia's second report of a UI-thread exception, logs a benign one at Debug, and any other at Error with the dialog, or at Warning without it when `ShowUnobservedTaskExceptions` is off |
| `InstallAvaloniaHooks()` | Called from `OnFrameworkInitializationCompleted`, where `Application.Current.ApplicationLifetime` is the app's lifetime |
| The runtime | Raises `TaskScheduler.UnobservedTaskException` on the finalizer thread, when a collection finalizes the faulted task. Templates' canary of 2026-11-13 forced a collection in the window's `Opened` handler to see one |
| The headless tests | Share one dispatcher, and `DoShutdown` shuts the dispatcher down in its `finally`, so no test here can raise a real lifetime's `Exit` |

## Decisions

| # | Decision | Why |
|---|---|---|
| 1 | **`AvaloniaDiagnosticsOptions.FailOnUnobservedTaskExceptions`, a `bool`, off by default and separate from the other two switches.** `bbavalonia` sets it to `App.Smoke` beside them | Named for what it does, as its siblings are: the maintainer kept `ShowUnobservedTaskExceptions` apart from `HandleUiThreadExceptions` on 2026-11-12. Off, since only a run proved by its exit code wants a collection forced at exit and its code changed |
| 2 | **The package counts every task exception nobody observed that `EntryPointOptions()`' callback finds not benign**, from the start of the run and whatever `ShowUnobservedTaskExceptions` says. Avalonia's second report of a UI-thread exception, and a benign exception, are not counted | The verdict that decides the log and stderr decides the exit code too. An exception reported early in the run is as much a failure as one the final collection finds |
| 3 | **With the switch on, `InstallAvaloniaHooks()` subscribes to the lifetime's `Exit`.** There it runs `GC.Collect()`, `GC.WaitForPendingFinalizers()`, `GC.Collect()`, `GC.WaitForPendingFinalizers()`; then, when the count is above zero and the exit code is 0, it sets the code to 1 and logs at Error how many were reported | `Exit` is the last place the app's code runs before the main loop ends, on the UI thread, and its args carry the code the process returns. The request's three steps and a fourth: a finalizer the second collection queues would otherwise run after the check. Nothing the package runs on the finalizer thread waits on the UI thread, so waiting there cannot deadlock it |
| 4 | **A code the app set is kept**: only 0 becomes 1 | The app's own failure code says more |
| 5 | **The code is 1** | The entry point's code for a run an exception ended. A smoke check reads only zero or not, and stderr already names each exception, as `AppMain` wrote it when it arrived |
| 6 | **A lifetime without `Exit`**, one that is not an `IControlledApplicationLifetime`, such as a single view's, gets a Warning in the log at install, and nothing else | There is no exit to hook, and the log says why the switch did nothing |
| 7 | **`AppMain` is unchanged** | The code flows out through the lifetime and the work's return value, and `AppServices.EntryPoint` references nothing |

### The API

```csharp
// Bennewitz.Ninja.AppServices.AvaloniaUI
public sealed class AvaloniaDiagnosticsOptions
{
    // …
    public bool HandleUiThreadExceptions { get; init; } = true;
    public bool ShowUnobservedTaskExceptions { get; init; } = true;
    public bool FailOnUnobservedTaskExceptions { get; init; }
}
```

`bbavalonia` adds one line beside its two:

```csharp
HandleUiThreadExceptions = !App.Smoke,
ShowUnobservedTaskExceptions = !App.Smoke,
FailOnUnobservedTaskExceptions = App.Smoke,
```

### Dismissed

- **One `SmokeRun` switch that sets the other two.** One line in a template, but it couples three
  behaviours, and a host setting it beside one of them would need a rule for which wins.
- **An exit hook on `AppMainOptions`.** It would serve console apps too, but it changes
  `AppServices.EntryPoint` for every kind of app, and the request is `AvaloniaDiagnostics`'.
- **The template forcing the collection itself.** The maintainer chose the package, so that every
  Avalonia app has it (2026-11-13).
- **The request's three steps alone.** A finalizer the second collection queues would raise its
  exception after the check.
- **A code of its own,** such as 3. A fifth code in the entry point's contract, which a smoke check
  would read as non-zero like any other.
- **A line of its own on stderr.** `AppMain` owns stderr, and every counted exception is already there.
- **Counting only what the final collection finds.** An exception reported earlier in the run is as
  much a failure.

## Limits

- A task still running at exit, or still referenced, is not collected, so its exception is not caught.
- An exception reported after the check, while the process ends, is not counted.
- A run that an exception ends already exits 1, and `Exit` may not be raised at all.
- The collection adds a pause at exit, measured in step 3.
- A host's `IsBenignException` that waited on the UI thread would hang the exit, since the filter runs
  on the finalizer thread while the UI thread waits for it. Its documentation already asks for a filter
  that is quick and thread-safe; the switch's says why.
- With `ShowUnobservedTaskExceptions` on as well, a failure the final collection finds is logged at
  Error and not shown, since the dispatcher is shutting down.

## Scope

**In:** decisions 1 to 7 and their tests; a probe that proves Avalonia's own wiring; `docs/appservices.md`
(the switch under "Task exceptions nobody observed"; an upgrading note giving a smoke run's three
lines; and the `2026.4.1112` note's sentence that an unobserved exception leaves the exit code alone,
which then holds only with the switch off); the doc comments on the three switches and on
`InstallAvaloniaHooks()`; the `AGENTS.md` rows that name them; `PROGRESS.md`; a release of the
AppServices family; and telling Templates.

**Out:** `AppServices.EntryPoint`; the templates beyond `bbavalonia`'s line; a console app's smoke run;
ScopedEditors.

## Steps

| # | Step | Verified by |
|---|---|---|
| 1 | This plan, committed on its own once approved | CI green |
| 2 | Avalonia: the switch; the count, in `EntryPointOptions()`' callback; `InstallAvaloniaHooks()` subscribing the exit handler once to an `IControlledApplicationLifetime`'s `Exit` when the switch is on, and logging a Warning when the lifetime has none; `ResetForTests` clearing both | Headless tests that call the exit handler with a constructed `ControlledApplicationLifetimeExitEventArgs`, since raising a real `Exit` shuts down the dispatcher the tests share. Every row of the shared table, abandoned in a task inside an `AppMain` run with the switch on, and found by the exit handler's own collection: code 1 when its unobserved verdict is not benign, 0 when it is benign. Avalonia's second report, 0; a code the app set, kept; an exception reported before `InstallAvaloniaHooks()`, counted; the switch off, the code left alone; a lifetime without `Exit`, the Warning. Each test seen red when broken on purpose. Package validation against `2026.4.1113` reports no breaking change |
| 3 | A desktop probe: an app under `tests/`, never packed, run as a child process on Avalonia's headless platform, as `EntryPointProbe` is run. It drops a faulted task and shuts down with `Shutdown(0)` | Exit 1 with the switch on and 0 with it off, so nothing is hooked when it is off; the exception on stderr both times; and the pause the collection adds, measured. The one proof of `DoShutdown`'s wiring. Measured first: whether `StartWithClassicDesktopLifetime` runs on the headless platform in such a probe. If it cannot, stop and report: the proof would then rest on the source and on Templates' canary after the release, which is the maintainer's call |
| 4 | `docs/appservices.md`; the doc comments; the invariants in `src/AGENTS.md` and the root `AGENTS.md`; `tests/AGENTS.md`; `repo-conventions grants` for the probe; `PROGRESS.md` | `repo-conventions check` conforms; CI green |
| 5 | Release the AppServices family at the day's version, on the maintainer's word, through `docs/publishing.md` | Every id in the nuget.org flat container and restored from nuget.org alone, then `PROGRESS.md`'s Published row |
| 6 | Tell Templates the version, and the line `bbavalonia` needs: `FailOnUnobservedTaskExceptions = App.Smoke` | Told |
