# 00003 — Unobserved task exceptions shown, as UI-thread failures are

> Status: **approved 2026-11-12**. Amends [`00002`](00002-ui-thread-exception-hook.md)'s decision 6;
> supersedes nothing. Asked for by ClaudeForge on 2026-11-12, the maintainer's choices of that day.

AppServices `2026.4.1112` logs a task exception nobody observed at Warning and shows nothing, as
`00002`'s decision 6 chose. ClaudeForge shows such an exception in its `FatalErrorDialog` today and
cannot keep doing so on top of the package: a wrapper around `OnUnobservedTaskException` cannot see
the package's verdict on Avalonia's second report of a UI-thread exception, so one real failure could
show two dialogs. Only the package knows that verdict, and only it owns the dialog's gate and repeat
counting.

## The requirement

| # | From ClaudeForge's request |
|---|---|
| a | A task exception nobody observed, when it is not benign, is logged at Error and shown in the `FatalErrorDialog`, marshalled to the UI thread |
| b | It goes through the UI-thread handler's one-at-a-time and repeat-counting machinery |
| c | It is never shown for Avalonia's second report of a handled UI-thread exception |
| d | A benign one is unchanged: Debug, no dialog |
| e | Made the behaviour rather than an option, since there are no other consumers yet; a flag defaulting to the dialog if a host needs none |

## What exists

| Source | What it shows |
|---|---|
| `AvaloniaDiagnostics.EntryPointOptions()` | Its `OnUnobservedTaskException` skips Avalonia's second report of a handled UI-thread exception, logs a benign exception at Debug and any other at Warning, and shows no dialog (`00002`'s decision 6) |
| `UiThreadExceptions` | Owns the one-at-a-time gate, the repeat table (a failure is its type, message and throw site; each has a count and whether it was offered its dialog), the `OpenDialog` seam and `WritePendingRepeats`. It records the dispatcher it subscribes to only when `HandleUiThreadExceptions` installs it |
| `InstallAvaloniaHooks()` | Called from `OnFrameworkInitializationCompleted`, on the UI thread. Subscribes the UI-thread handler to `Dispatcher.UIThread` when `HandleUiThreadExceptions` is set |
| Second-report recognition in `2026.4.1112` | The handler remembers an exception only once it has handled it: `Remember` runs after its early return for an exception already `Handled`, and nothing is subscribed when `HandleUiThreadExceptions` is false. So an exception another handler dealt with first is left alone, its second report included, which `EntryPointOptions()` then logs at Warning (`PROGRESS.md`, drift from `00002`) |
| `00002`'s ways out for an app with a UI-thread handler of its own | Set `IsBenignException` and delete the app's handler; set `HandleUiThreadExceptions = false` and keep it; or subscribe it before `InstallAvaloniaHooks()`. The second and third keep the app's own handler, and its own dialog |
| The runtime | Raises `TaskScheduler.UnobservedTaskException` on the finalizer thread, at whichever garbage collection finds the task, with the task's exceptions wrapped in an `AggregateException` |
| Avalonia 12.1.3's `Dispatcher.UIThread` | Read on a thread where no UI dispatcher lives, it creates one bound to that thread (measured 2026-11-11; the comment in `UiThreadExceptions.ResetForTests`) |
| `EntryRun.OnUnobservedTaskException` | Writes a non-benign one to stderr, then calls the callback |
| ClaudeForge | Its own unobserved handler counts an exception that is nothing but cancellations as benign, and logs any other at Error with the dialog. The filter it will pass as `IsBenignException` leaves cancellations out, since it shows them when they escape on the UI thread (`00002`, "The API"). It holds its AppServices bump until a release carries this plan |
| `bbavalonia`, in JanusMael/Bennewitz.Ninja.Templates#94 (unreleased) | Reads `--smoke` before `ConfigureLogging` and sets `HandleUiThreadExceptions = !App.Smoke`. With the handler left on, a smoke run whose window threw hung, printing nothing, until killed at 120 s, most likely held open by the dialog |

## Decisions

| # | Decision | Why |
|---|---|---|
| 1 | **`AvaloniaDiagnosticsOptions.ShowUnobservedTaskExceptions`, a `bool`, on by default and separate from `HandleUiThreadExceptions`** (the maintainer's choices, 2026-11-12). The options `EntryPointOptions()` returns read it when an exception arrives, as they read `IsBenignException` | `HandleUiThreadExceptions` decides whether a UI-thread exception ends the process; this decides only whether an unobserved one is shown, and an unobserved one never ends it. On by default: a background save, backup or fetch that fails unobserved is otherwise silent in a desktop app, and ClaudeForge, the one consumer, needs no setting |
| 2 | **With it on, `EntryPointOptions()`'s callback logs a non-benign unobserved task exception at Error and shows it in the `FatalErrorDialog`** (amends `00002`'s decision 6). With it off, Warning and no dialog, as in `2026.4.1112`. A benign one stays at Debug with no dialog either way, and Avalonia's second report of a handled UI-thread exception is neither logged nor shown | Requirements a, c and d. Error, because the user is now told of it, as of a UI-thread failure. Off logs at Warning with no dialog, as `2026.4.1112` did, for a host that wants none |
| 3 | **The dialog goes through the UI-thread handler's machinery:** its one-at-a-time gate, its repeat table and its dialog seam. A failure is offered one dialog whichever path reports it, and its repeats are counted, never logged in full again. The failure counted and shown is the aggregate's one inner exception when it has exactly one, as a task that failed on one throw does; otherwise, as for a `Task.WhenAll` that several failures faulted, the aggregate itself | Requirement b. A second gate would show two dialogs at once, and a second table would show one failure twice when it came both ways. The runtime wraps every unobserved exception, so the wrapper's type and message would make every one the same failure |
| 4 | **One subscription whenever either switch is on.** `InstallAvaloniaHooks()` records the UI thread's dispatcher and subscribes one handler to its `UnhandledException` when either switch is on. The handler remembers every exception the dispatcher reports, whoever handles it, and handles them (sets `Handled`, logs, shows) only when `HandleUiThreadExceptions` is set. So the second report of an exception an app's own handler dealt with is recognised too, and neither logged nor shown | Requirement c, for every app. On `00002`'s second or third way out, the app's own handler shows its dialog for a UI-thread failure; without this, the package would show another for the same failure's second report. The cost: that second report, which `2026.4.1112` logged at Warning, is not logged at all, since the app's handler already reported the failure |
| 5 | **The dialog opens on the UI thread, through the recorded dispatcher,** posted from the finalizer thread the callback runs on. The posted job takes the one-at-a-time gate on the UI thread, so a dialog the UI-thread handler opened in the meantime still holds it. Before `InstallAvaloniaHooks()` has run, and once the app has shut down, the exception is logged and not shown | A window can only be created on the UI thread. Read on the finalizer thread before Avalonia has started, `Dispatcher.UIThread` would make a dispatcher bound to the finalizer thread, so the callback never reads it. Taken on the finalizer thread, the gate could be passed by a UI-thread dialog opening before the posted job runs, and two would show. Posting returns at once, so finalization never waits for the user |
| 6 | **`AppMain` is unchanged.** Its stderr report of a non-benign unobserved task exception stays, and `IsBenignException` still keeps a benign one off stderr | The dialog is the Avalonia package's business, and `AppServices.EntryPoint` references nothing |
| 7 | **An unobserved exception that is nothing but cancellations is benign, on that path only** (the maintainer's choice, 2026-11-12). The filter `EntryPointOptions()` passes on also calls an unobserved exception benign when everything it flattens to is an `OperationCanceledException`, `TaskCanceledException` included, whatever the switch says: it is logged at Debug, shown nothing, and kept off stderr. One beside a real failure is not benign. A cancellation that escapes on the UI thread is untouched, and the host's filter still governs everything else on both paths | ClaudeForge's own unobserved handler counts such an exception as benign today, and its filter must leave cancellations out, since it shows them on the UI thread; without this, adopting the package would turn every cancellation nobody awaited into a fatal-error dialog. A cancellation found by a garbage collection is abandoned work, not a failure the user can act on |

### The API

```csharp
// Bennewitz.Ninja.AppServices.AvaloniaUI
public sealed class AvaloniaDiagnosticsOptions
{
    // …
    public bool HandleUiThreadExceptions { get; init; } = true;
    public bool ShowUnobservedTaskExceptions { get; init; } = true;
    public Func<Exception, bool>? IsBenignException { get; init; }
}
```

ClaudeForge sets nothing new: it takes the version and makes the one-commit change `00002` described,
with no `OnUnobservedTaskException` of its own. `bbavalonia` adds one line beside its smoke line:

```csharp
HandleUiThreadExceptions = !App.Smoke,
ShowUnobservedTaskExceptions = !App.Smoke,
```

### Dismissed

- **Tying it to `HandleUiThreadExceptions`.** One line in `bbavalonia` instead of two, but it couples
  whether a UI-thread exception ends the process with whether an unobserved one is shown (the
  maintainer, 2026-11-12).
- **Off by default.** It keeps `2026.4.1112` for every app that does not opt in, but a silent
  background failure is the case for the dialog (the maintainer, 2026-11-12).
- **A public check for Avalonia's second report, leaving the dialog to each host.** Every host would
  build its own gate and repeat counting, and a second gate shows two dialogs at once.
- **A gate and repeat table of the unobserved path's own.** One failure reported both ways would be
  shown twice, and two dialogs could be open at once.
- **Remembering only what the package's handler handles, as `2026.4.1112` does.** An app on `00002`'s
  second or third way out would see two dialogs for one failure.
- **Reading `Dispatcher.UIThread` from the callback.** Before Avalonia starts, it would bind a new
  dispatcher to the finalizer thread.
- **Waiting on the finalizer thread until the dialog is dismissed.** Every finalizer in the process
  would wait with it.
- **Showing an unobserved cancellation like any other failure.** Symmetric with the UI thread, but
  ClaudeForge would gain a dialog for every cancellation nobody awaited (the maintainer, 2026-11-12).
- **Leaving cancellations to the host's filter.** ClaudeForge would have to add them to it, which also
  stops it showing the cancellations that escape on the UI thread.

## Scope

**In:** decisions 1 to 7, their tests, `docs/appservices.md` (the unobserved path beside the UI-thread
table, and an "Upgrading from 2026.4.1112" note: the dialog is on by default, `false` keeps
`2026.4.1112`'s Warning and no dialog, a smoke run sets it to `!smoke`, an app that shows its own
dialog for these drops it or shows two, the second report of a UI-thread exception an app's own
handler dealt with is no longer logged, and an unobserved exception that is nothing but cancellations
is benign), the doc comments on `ShowUnobservedTaskExceptions`, `HandleUiThreadExceptions` and
`EntryPointOptions()`, the `AGENTS.md` rows that name them, `PROGRESS.md`, a release of the
AppServices family, and telling ClaudeForge and Templates.

**Out:** ClaudeForge's own change; the templates beyond `bbavalonia`'s one line; `AppServices.EntryPoint`;
ScopedEditors; a non-fatal dialog for these exceptions.

## Steps

| # | Step | Verified by |
|---|---|---|
| 1 | This plan, committed on its own once approved | CI green |
| 2 | Avalonia: `ShowUnobservedTaskExceptions`; `InstallAvaloniaHooks()` records the UI dispatcher and subscribes the one handler whenever either switch is on, remembering every exception and handling only when `HandleUiThreadExceptions` is set; `EntryPointOptions()`'s callback logs and shows as decisions 2, 3 and 5 say, through `UiThreadExceptions`; its filter calls an all-cancellation unobserved exception benign (decision 7) | Headless tests over `00002`'s shared table, which gains the cancellation cases with each path's verdict: an unobserved exception that is nothing but cancellations is logged at Debug, shown nothing and kept off stderr whatever the switch says, one beside a real failure is not, and a cancellation that escapes on the UI thread is still shown. Each row's exception, wrapped as the runtime wraps it and passed to the callback from a thread that is not the UI thread, is logged at Debug with no dialog when benign, and otherwise at Error with the dialog requested on the UI thread, a filter's throw logged beside it. With the switch off, Warning and no dialog. Avalonia's second report: nothing, whether the package's handler, an app's own handler subscribed first, or an app's handler with `HandleUiThreadExceptions` off dealt with the exception; and with it off, an exception nothing handles still escapes `RunJobs()`. The tests that pin `2026.4.1112`'s opposite, the last assertion of `An_exception_another_handler_already_handled_is_left_alone` and `False_installs_nothing`, change with it and are named for what they then assert. While a UI-thread dialog is open, an unobserved failure is logged and not shown, and shown at its first repeat once that dialog closes; the same failure from either path is one failure. Before `InstallAvaloniaHooks()`, logged, not shown, nothing thrown. One row raised for real: a task abandoned and collected inside one `Session.Dispatch`, inside an `AppMain` run, whose `RunJobs()` then opens the dialog, since a dispatcher the session has moved past runs no more jobs. Each test seen red when broken on purpose; package validation against `2026.4.1112` reports no breaking change |
| 3 | `docs/appservices.md`, with the upgrading note; the doc comments; `src/AGENTS.md` and the root `AGENTS.md` invariants; `PROGRESS.md` | `repo-conventions check` conforms; CI green |
| 4 | Release the AppServices family at the day's version, on the maintainer's word, through `docs/publishing.md` | Every id in the nuget.org flat-container and restored from nuget.org alone, then `PROGRESS.md`'s Published row |
| 5 | Tell ClaudeForge the version, for its one-commit change with no unobserved handler of its own. Tell Templates the version, and the line `bbavalonia` needs in the same sync, `ShowUnobservedTaskExceptions = !App.Smoke` | All told |
