# Progress

Work state for Bennewitz.Ninja.AppKit, updated in the same change as the work it describes. What has
stopped changing moves out rather than piling up.

## Resume

The repository grew from Bennewitz.Ninja.AppServices under `plans/00009` step 8 in
Bennewitz.Ninja.Templates: ScopedEditors' history was imported whole, and the two families now share
one solution, one set of pins and one CI. Both families first released from here on 2026-11-10, at
`2026.4.1110`, on the maintainer's word (see Published).

DiffView does not live here. The plan brought it too, and the maintainer took it out on 2026-11-06:
development stays in Bennewitz.Ninja.DiffView, which is not archived.

**Synced to Bennewitz.Ninja.Templates `2026.4.1111`** on 2026-11-11, on the maintainer's word.

- **The conventions script is that release's.** `scripts/repo-conventions.cs` now also fails a
  repository whose release does not resolve through `release-target.cs`, and one that does not pin
  AssemblyQuality. This repository meets both.
- **`Bennewitz.Ninja.CodeQuality` `2026.4.1104` is referenced privately**, every rule at its default,
  the family's decision of 2026-11-10. It reported two `BNCQ1009` findings, both in tests and neither
  able to end a process unintentionally, and each is suppressed where it stands with its reason:
  - `EntryPointProbe`'s `background` thread throws on purpose;
  - `EntryPointWebProbeTests`' thread catches the one exception its invoke can throw.
- **The pins are as the sync to `2026.4.1109` left them.**
  - AutoVersioning `2026.4.1031` and xunit.v3 `4.0.3`.
  - In the tests, AssemblyQuality `2026.4.1031` and XamlQuality `2026.4.1109`.
  - Avalonia stays `12.1.3`, the floor every consumer inherits.
- **Verified on Windows:**
  - the build passed with warnings as errors;
  - 468 tests, 453 passed and 15 skipped as before;
  - pack and `assert-packages` passed;
  - `check` conforms.

  The AOT and trim jobs run in CI.

Neither family's packages changed, so the sync released nothing.

**ScopedEditors runs `BNXQ1009` and `BNXQ1010`** from 2026-11-09, by the owner's choice, in
`tests/ScopedEditors.Tests/Accessibility/GeneratedRowRuleTests.cs`: the rows a list generates are
named by what they show and draw a background of their own. Each reads the strict enum's `ComboBox`,
the one such list, with the package's assembly loaded, and requires something inspected and nothing
skipped. Canaried: the row's `TextBlock` wrapped in a `Border` fails `BNXQ1009` alone, and the rows'
`Background` set to `{x:Null}` fails `BNXQ1010` alone. XamlQuality's other unadopted rules,
`BNXQ1003` to `BNXQ1006` and `BNXQ1008`, were measured the same day and inspect nothing here: no
control themes or templates, no key bindings, no fixed or zero-size grid slots. Tests only; no
package changes.

**`plans/00002`, the UI-thread exception hook, shipped in AppServices `2026.4.1112`** (approved
2026-11-11; its steps 2 to 6, see Published). `InstallAvaloniaHooks()` installs a handler on the UI thread's
`Dispatcher.UnhandledException`, on by default and off with `HandleUiThreadExceptions = false`. It
marks every exception handled, logs a benign one at Debug, and logs any other at Error and shows it
in a `FatalErrorDialog`, one dialog at a time; a repeat is counted rather than logged, with a count
line when its dialog closes and when `FlushLog` runs. `AvaloniaDiagnosticsOptions.IsBenignException`
is the one filter: the handler reads it, and `EntryPointOptions()` passes it on as
`AppMainOptions.IsBenignException`, which keeps a benign task exception nobody observed off stderr
while the callback still receives it. `AppMainOptions` is a `sealed record`. The rule that applies a
filter, the plan's decision 3, is copied into each package as an internal `BenignExceptionRule`, and
both copies are held to one table, `tests/AppServices.Tests/BenignExceptions/BenignExceptionTable.cs`.
Each new test was seen red with the code it guards broken on purpose. One rule has its code and
comment for its only guard: that `ResetForTests` leaves the dispatcher recorded at install, never
`Dispatcher.UIThread`. No headless test can tell the two apart, since a dispatcher the session has
moved past runs no more jobs (measured). Build with warnings as errors,
559 tests (544 passed, 15 skipped as before), and `check`, which conforms, pass on Windows. Package
validation against `2026.4.1110` reports no breaking change in `.EntryPoint` or `.Avalonia`, and
reports a planted one (`CP0002`); the native AOT probe publishes on win-x64 without a warning and
passes its contract there, and CI's `aot-linux` covers Linux. **A change of behaviour for an app
with a UI-thread handler of its own:** `docs/appservices.md`, "Upgrading from 2026.4.1110", gives the
cost and the three ways out.

**Avalonia reports each UI-thread failure a second time, and the package knows it.** Its 12.1.3
`DispatcherOperation.InvokeCore` puts a posted job's exception in the job's task before
`UnhandledException` sees it, and nothing reads that task, so the runtime raises the exception again,
at whichever later collection finds the task, as a task exception nobody observed (traced in a test
run on 2026-11-11). CI's Windows run caught it in the tests: the handler's tests left those exceptions
behind, and the web probe's run reported one on its stderr. They now collect them before each test
ends (`tests/AGENTS.md`). In an app that `AppMain` runs with `EntryPointOptions()`, every failure,
each repeat included, would have been logged again at Warning and written on stderr. By the
maintainer's choice of 2026-11-11, the handler remembers, weakly, each exception it handles, and
`EntryPointOptions()` keeps that second report off stderr and out of the log (see Drift); since
`plans/00003` it remembers every exception the dispatcher reports, whoever handles it. Every row of
the shared table is thrown for real inside an `AppMain` run and its second report collected there, and
each of seven planted breaks turns a test red.

**Avalonia and drivable-UI lessons go to Inspections** from 2026-11-12, by the maintainer's choice:
the root `AGENTS.md` carries every template's paragraph, which it had lacked since the merge.
Four went to the XamlQuality session that day, for `docs/avalonia-gotchas.md`, each read in Avalonia
12.1.3's source or seen in this repository's tests: its second report of a UI-thread exception; a
posted job's cancellation, reported as a failure twice; a post to a dispatcher that has shut down,
dropped without a word; and `Dispatcher.UIThread`, read on another thread before any dispatcher
exists, binding the UI thread there. A correction went with them: the second report was seen in a
run hosting a headless session, not in an application on a real platform. All of it is in
JanusMael/Bennewitz.Ninja.Inspections#139, still open on 2026-11-13.

**ScopedEditors runs `BNAQ1005`** from 2026-11-12, by the maintainer's choice, so both families'
tests run `BNAQ1001` to `BNAQ1005`; until then nothing checked what ScopedEditors' compiled assemblies
grant. A stray grant planted in `ScopedEditors.Abstractions` failed it alone, 1 test of 270.
CodeQuality came with the `2026.4.1111` sync at its defaults, the family's decision of 2026-11-10, so
`BNCQ1001` stays off where `BNAQ1001` already holds; `BNAQ1006` stays unadopted.

**`plans/00003`, task exceptions nobody observed shown as UI-thread failures are, shipped in
AppServices `2026.4.1113`** (approved 2026-11-12; its steps 1 to 5, see Published). ClaudeForge asked for it: a wrapper of its own
around the callback cannot tell Avalonia's second report apart, so one failure could show two
dialogs. `AvaloniaDiagnosticsOptions.ShowUnobservedTaskExceptions`, on by default and separate from
`HandleUiThreadExceptions` by the maintainer's choice, has `EntryPointOptions()`' callback log a task
exception nobody observed, when not benign, at Error and show it in a `FatalErrorDialog`, through the
UI-thread handler's gate and repeat table. The dialog is posted from the finalizer thread to the
dispatcher `InstallAvaloniaHooks()` recorded. Off, it is logged at Warning with no dialog, as in
`2026.4.1112`. `InstallAvaloniaHooks()` subscribes the handler while either switch is on, and the
handler remembers every exception the dispatcher reports, so Avalonia's second report is known in an
app with a UI-thread handler of its own too. On that path alone, an exception that is nothing but
cancellations is benign, by the maintainer's choice: logged at Debug, shown nothing, kept off stderr.
Build with warnings as errors, 649 tests (634 passed, 15 skipped as before), and `check`, which
conforms, pass on Windows. Each new test was seen red with the code it guards broken on purpose: 18
of 19 planted breaks turned the tests aimed at them red. The 19th, posting to `Dispatcher.UIThread`
read on the finalizer thread once the hooks are installed, survived and is equivalent there, since
`Dispatcher.UIThread` is then the UI thread's dispatcher from any thread (measured: the dialog still
opened on the UI thread); falling back to it before they are installed turned
`Before_InstallAvaloniaHooks_a_task_exception_is_logged_and_not_shown` red. That nothing is shown once
the app has shut down rests on Avalonia 12.1.3's `Dispatcher.InvokeAsyncImpl`, which enqueues nothing
for a dispatcher that has shut down and aborts the post without a throw (read in its source): no test
shuts down the headless dispatcher, which every later test shares. Package validation against
`2026.4.1112`, packed as the next day's build, reports no breaking change in `.EntryPoint` or
`.Avalonia`. `docs/appservices.md` gains "Upgrading from 2026.4.1112".

**Next, in order:**
1. **ClaudeForge's change is its own** (`plans/00002` step 6 and `plans/00003` step 5, told on
   2026-11-12 and 2026-11-13): in one commit, take `2026.4.1113`, set its classifier as
   `IsBenignException`, and delete its UI-thread handler, its unobserved handler and its rebuilt
   options; it needs no unobserved handler of its own. OpenForge2k's copy of ClaudeForge holds
   `2026.3.924` until it can make the same change.
2. **Templates' change is its own** (`plans/00003` step 5, told on 2026-11-13): `bbavalonia` takes
   `2026.4.1113` with `ShowUnobservedTaskExceptions = !App.Smoke` beside its
   `HandleUiThreadExceptions = !App.Smoke`. Its `main` pins `2026.4.1112` since
   JanusMael/Bennewitz.Ninja.Templates#95, which brought the UI-thread line; Templates has released
   nothing since `2026.4.1111`.
3. **Each family's next release** is a family release, `appservices/v…` or `scopededitors/v…`, on the
   maintainer's word when the family next changes. The policy names this repository and covers all
   eight ids: ScopedEditors' three pushed at `2026.4.1110`, AppServices' five at `2026.4.1113`.

## Drift from `plans/00002`

- **"Never shown twice" is one dialog for each failure.** A failure is offered its dialog at its
  first occurrence, or, when another dialog is open then, at its first repeat after that dialog
  closes: as the maintainer's choice of 2026-11-11 put it, a different exception still gets its
  dialog, one at a time. A dialog that fails to show counts as offered, so a dialog that cannot open
  is not tried, and its failure logged, again on every repeat.
- **Avalonia's second report of a UI-thread exception is not reported again.** The plan did not know
  of it. By the maintainer's choice of 2026-11-11, `UiThreadExceptions` remembers, weakly, each
  exception it handles and what an aggregate among them flattens to; the filter `EntryPointOptions()`
  passes on calls that second report benign, and its callback skips it. An exception another handler
  dealt with first is left alone, its second report included. One case still reaches stderr: an empty
  `AggregateException` thrown on the UI thread flattens to nothing, and the entry point's rule calls
  that not benign before any filter runs.
- **The throw site is the first line of the exception's stack trace, as text.** The plan does not say
  how a repeat's throw site is read. `TargetSite` and `StackFrame.GetMethod` need metadata a trimmed
  app may not keep, and the trim analyser fails the build on them.
- **What the plan leaves open, settled here:** a repeat's count line is logged at Error, as the
  failure it counts; a benign exception is logged at Debug every time, since the repeat rule governs
  what is reported; the handler logs a filter's throw at Warning beside the Error line, and not again
  for a repeat; a dialog that fails to show is logged at Error. On stderr, a filter's throw follows the
  exception's own report as `warning: <app>: the IsBenignException filter threw`.

## Drift from `plans/00003`

- **A repeat's count line begins "An exception repeated"**, where `2026.4.1112`'s began "A UI-thread
  exception repeated": a failure may now come either way. The plan does not say what the line reads;
  the upgrading note does.
- **More tests changed than the plan names.** Besides `False_installs_nothing` and the last assertion
  of `An_exception_another_handler_already_handled_is_left_alone`, two tests in
  `AvaloniaDiagnosticsEntryPointTests` pinned `2026.4.1112`'s Warning: each asserts Error now, a twin
  asserts Warning with the switch off, and the table's theory runs every row both ways.
- **Every row is raised for real, both ways**, where the plan asks for one: each is abandoned in a task
  and collected inside one `Session.Dispatch` inside an `AppMain` run, with the switch on and off. The
  second-report tests collect and run the dispatcher's jobs inside the dispatch as well, so that a
  dialog posted in error would be requested and seen.
- **What the plan leaves open, settled here:** before `ConfigureLogging` has run, the switch reads its
  default, on, though nothing can be shown before `InstallAvaloniaHooks()`; the Error line carries the
  exception as the runtime raised it, wrapped, and the dialog the failure; a filter's throw is logged
  at Warning beside it, as on the UI thread; a dialog that cannot be posted is logged at Error; a
  dialog posted for a failure that has been offered one since opens nothing; and the cancellation
  rule asks the host's filter nothing about a cancellation and lives in `EntryPointOptions()`' filter,
  in neither copy of `BenignExceptionRule`.
- **`bbavalonia`'s smoke line came in JanusMael/Bennewitz.Ninja.Templates#95**, where the plan's
  "What exists" has #94: both merged on 2026-11-12, and Templates has released neither.

## What the merge changed for consumers

Shipped at `2026.4.1110`, each family's first release from here. Against `2026.4.1103`, the packages'
dependencies and files are identical; only these change:

| Family | Change at `2026.4.1110` |
|---|---|
| AppServices | `RepositoryUrl` and `PackageProjectUrl` name AppKit, and the packed README is `docs/appservices.md` |
| ScopedEditors | `RepositoryUrl` and `PackageProjectUrl` name AppKit, and the packed README is `docs/scopededitors.md` |

## Published

### AppServices

All five ids ship together at every version.

| Version | Tag | Verified |
|---|---|---|
| `2026.4.1113` | `appservices/v2026.4.1113` | `plans/00003`; release run `43448071372`. All five ids listed by the nuget.org flat-container with a nuspec naming AppKit at `d14b1c5`, and a project with nuget.org as its only source restored them |
| `2026.4.1112` | `appservices/v2026.4.1112` | `plans/00002`; release run `43253366608`, after GitHub failed to start the 2026-11-11 preflights (`startup_failure`, an internal error). All five ids listed by the nuget.org flat-container with a nuspec naming AppKit at `44438c7`, and a project with nuget.org as its only source restored them |
| `2026.4.1110` | `appservices/v2026.4.1110` | First from AppKit; release run `42842847404`. All five ids listed by the nuget.org flat-container with a nuspec naming AppKit at `a1f42db`, and a project with nuget.org as its only source restored them |
| `2026.4.1103` | `v2026.4.1103` | All five ids listed by the nuget.org flat-container, and a project with nuget.org as its only source restored them |
| `2026.4.1031` | `v2026.4.1031` | Listed; release run `41231829335` |
| `2026.4.1001` | `v2026.4.1001` | Listed; a console app restoring from nuget.org alone built and ran through `AppMain` |
| `2026.3.924` | `v2026.3.924` | Listed |
| `2026.3.923` | `v2026.3.923` | Listed |

### ScopedEditors

The three ids ship together at every version.

| Version | Tag | Verified |
|---|---|---|
| `2026.4.1110` | `scopededitors/v2026.4.1110` | First from AppKit; release run `42842847901`. All three ids listed by the nuget.org flat-container with a nuspec naming AppKit at `a1f42db`, and a project with nuget.org as its only source restored them |
| `2026.4.1103` | `scopededitors/v2026.4.1103` | All three ids listed by the nuget.org flat-container, and a project with nuget.org as its only source restored them |
| `2026.4.1031` | `scopededitors/v2026.4.1031` | Listed; release run `41231920267` |
| `2026.3.924` | `scopededitors/v2026.3.924` | Listed. OpenForge2k consumes it from nuget.org |
| `2026.3.923` | `scopededitors/v2026.3.923` | Listed |

## Next, by family

**AppServices**
1. **Headless isolation per assembly** is unverified and waits for evidence before
   `[assembly: AvaloniaTestIsolation(AvaloniaTestIsolationLevel.PerAssembly)]` is considered.
2. `LiveLogWindowSink` and its windows stay in OpenForge2k until they can ship together; nothing here
   is scheduled for them.

**ScopedEditors**
1. **Per-assembly headless isolation is unverified**, as above. It is the candidate fix for the
   cross-thread failure seen on OpenForge2k's CI, and OpenForge2k has not reproduced that failure since
   its xUnit move (measured 2026-09-25) and parked its own attempt because it broke a seam, so the
   evidence has not arrived.

## The merge

Built on a branch, in a fresh clone of AppServices at `0c2dfae`, importing ScopedEditors at `8128f90`.

**What the plan did not foresee:**

- **AppServices and ScopedEditors run on Windows and macOS in CI for the first time**, in the one
  `build` matrix.
- **`git filter-repo` strips commit signatures**, so ScopedEditors' commit hashes changed although only
  its tags were renamed. Its archived repository keeps the original commits.
- **nuget.org's policy cannot change its repository.** The rename needed a new policy naming AppKit,
  and AppServices' old one deleted.
- **Lines from before the merge still said the root `README.md` is packed.** Each family packs its
  own `docs/<family>.md` as `README.md`. The root `AGENTS.md`, `docs/AGENTS.md` and `src/AGENTS.md`
  say so since 2026-11-11, and `docs/AGENTS.md` names AppKit as the policy's repository, where it
  still named ScopedEditors' old one.
