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

**Synced to Bennewitz.Ninja.Templates `2026.4.1114`** on 2026-11-16, on the maintainer's word.

- **`Bennewitz.Ninja.CodeQuality` rises from `2026.4.1111` to `2026.4.1113`**, every rule at its
  default, the family's decision of 2026-11-10. Its one change: `BNCQ1009` and `BNCQ3001` follow a
  delegate held in a local to its body, up to eight locals. It reports nothing new here. The two
  `BNCQ1009` findings of the `2026.4.1111` sync stay suppressed where they stand, each with its
  reason:
  - `EntryPointProbe`'s `background` thread throws on purpose;
  - `EntryPointWebProbeTests`' thread catches the one exception its invoke can throw.
- **The scripts are unchanged.** `scripts/release-target.cs` and `scripts/repo-conventions.cs` match
  the template's at `v2026.4.1114` by SHA-256; neither changed since `2026.4.1113`.
- **`docs/publishing.md` already describes the version floor**, since 2026-11-13, beside the quarter
  rule and in two rows of its failure table. Templates' runbook gained the same in `2026.4.1114`, in
  its own words; this one keeps its wording, which says the same.
- **No other pin moved.**
  - AutoVersioning `2026.4.1031` and xunit.v3 `4.0.3`.
  - In the tests, AssemblyQuality `2026.4.1031` and XamlQuality `2026.4.1109`. XamlQuality
    `2026.4.1111` came after the sync, as a change of its own: `plans/00005`, below.
  - Avalonia stays `12.1.3`, the floor every consumer inherits.
- **Verified on Windows, CI's jobs step by step:**
  - every project restored CodeQuality `2026.4.1113`;
  - the build passed with warnings as errors, 0 warnings;
  - 649 tests, 634 passed and 15 skipped as before;
  - pack and `assert-packages` passed, 8 packages;
  - `check` conforms;
  - the native AOT probe published on win-x64 without a warning and passed its contract, 18 tests
    with 4 skipped for Windows' signals;
  - the trimmed publish matched its baseline, 3 expected warnings and none new.

Neither family's packages changed, so the sync released nothing.

**`reference/` holds read-only checkouts of the upstream sources at the pinned versions** from
2026-11-16, by the maintainer's choice: every repository that reads upstream library sources keeps
them pinned there, so an agent greps the exact version the build uses instead of fetching single files
through `gh api`. `reference/fetch-reference.cs` merges DiffView's and XamlQuality's scripts of that
name, and fetches each source at the commit its packages' nuspecs record for the pinned version,
ClaudeForge's idea. `reference/sources.json` lists Avalonia 12.1.3 (`src` and `tests`),
Avalonia.Controls.DataGrid 12.1.2 and Semi.Avalonia 12.1.0 (`src`). The script, its manifest, the
README, `AGENTS.md` and three MSBuild shields are tracked; `/reference/*/` in `.gitignore` ignores the
checkouts, and the script fetches nothing until git ignores each one. It builds under the shields with
the settings its `#:property` lines state, so it builds alike in any repository that keeps it.
Verified on Windows:

- a fresh fetch of all three in 43 s, each HEAD the commit its nuspecs record, every text file LF,
  and each checkout's own `git status` clean;
- `off-pin` for an edited pin record and for a moved HEAD, each fetched again by `--update`;
- exit 2 for each usage error, a `dir` outside `reference/`, a tag without `{version}`, a package
  nothing pins, packages that disagree on version or on commit, a `url` the nuspecs contradict, and
  `.gitignore` without the line;
- the same pins with the global packages folder empty, read from nuget.org;
- a commit pin, a tag, a `local` sibling, and a sibling that lacks the pin, which falls back to the
  url;
- in a throwaway repository without central package management, Semi.Avalonia 12.1.0.1 from a csproj,
  fetched at the commit its nuspec records, and refused once a second csproj pins 12.1.0;
- a warning planted in the script fails its build under the shields, and this repository's analyzers
  never reach it;
- the solution's build with warnings as errors, and `check`, which conforms.

Nothing ships, and neither family's packages changed.

**Templates is the reference kit's canonical home** from 2026-11-17, by the maintainer's word
(JanusMael/Bennewitz.Ninja.Templates#108). There the checkouts live under `reference/src/<name>`,
ignored by the one line `/reference/src`, and on Windows the script refuses to fetch until long paths
are enabled. JanusMael/Bennewitz.Ninja.Templates#109, merged on 2026-11-19, makes
`repo-conventions check --repo` require that line of a repository that keeps the kit, and Templates
released it in its `2026.4.1119` the same day. This repository runs its own copy of `repo-conventions.cs`,
so its CI and its release stay green until it takes the layout, though Templates' `check --repo` reports
it on `reference` and `drift` until then. By the maintainer's choice of 2026-11-17 it takes it in a pull
request of its own, now that Templates has released #109, in one change:
- the script, its shields and `repo-conventions.cs` from Templates, since its own `check` fails without
  the last;
- `/reference/src` in `.gitignore` in place of `/reference/*/`, and each `dir` in `sources.json` under
  `reference/src/`, the checkouts moved or fetched again;
- the paths in `reference/README.md`, `reference/AGENTS.md`, the root `AGENTS.md` and here.

Three repositories took the script from here before that:
- DiffView (JanusMael/Bennewitz.Ninja.DiffView#59), whose CI ran it on Linux, macOS and Windows, where
  this repository had measured it on Windows alone;
- Inspections (JanusMael/Bennewitz.Ninja.Inspections#145), where `--update` migrated its old one-line
  records;
- ClaudeForge (JanusMael/ClaudeForge#166), with Semi.Avalonia 12.1.0.1.

A checkout reached through a symbolic link was never accepted here, whatever the ignore line: git
refuses a pathspec beyond a link, and the script exits 2. Templates' layout fixes it, with a test.

**ScopedEditors runs `BNXQ1009` and `BNXQ1010`** from 2026-11-09, by the owner's choice, in
`tests/ScopedEditors.Tests/Accessibility/GeneratedRowRuleTests.cs`: the rows a list generates are
named by what they show and draw a background of their own. Each reads the strict enum's `ComboBox`,
the one such list, with the package's assembly loaded, and requires something inspected and nothing
skipped. Canaried: the row's `TextBlock` wrapped in a `Border` fails `BNXQ1009` alone, and the rows'
`Background` set to `{x:Null}` fails `BNXQ1010` alone. XamlQuality's other unadopted rules,
`BNXQ1003` to `BNXQ1006` and `BNXQ1008`, were measured the same day and inspect nothing here: no
control themes or templates, no key bindings, no fixed or zero-size grid slots. Tests only; no
package changes.

**`plans/00005`, a tooltip on every input control, shipped in ScopedEditors and AppServices
`2026.4.1116`** (approved 2026-11-16; its steps 2 to 6, see Published). XamlQuality rises to
`2026.4.1111`, test-only, and ScopedEditors runs its
`BNXQ1011` in `tests/ScopedEditors.Tests/Accessibility/AxamlTooltipCoverageTests.cs`: 12 input controls
inspected, nothing skipped, nothing found, where ten had no tooltip. Each of
`PropertyEditorWrapper.axaml`'s six value inputs shows the property's `Description`, or its
`DisplayName` when that is missing or blank, through `FirstNonBlankConverter`, new and public in the
Avalonia package, and sets `ToolTip.ShowOnDisabled`, so a locked row shows it too.
`Controls/PropertyEditorWrapperTooltipTests` reads it off a rendered row of each editor, editable and
locked, with a description, without one and with a blank one. Browse, a string array's ×, its new-item
box and Add take four new `WrapperStrings` keys: `TipBrowse`, `TipRemoveItem`, `TipNewItem` and
`TipAddItem`. AppServices has no markup for the rule to read: there, `FatalErrorDialog` and
`NonFatalNoticeDialog` give Close, Copy to Clipboard and the details box a tooltip each, and
`DialogAccessibilityTests` requires one of every control its walker requires a name of. Each guard was
seen red with its defect planted, and green once it was removed:

- a value input's tooltip removed fails `BNXQ1011` and the rendered row;
- a fixed control's removed fails `BNXQ1011` alone;
- the first again, with a `CheckBox` style's tooltip setter planted, fails `BNXQ1011` on `Skipped`;
- a tip bound to `Description` alone, `ShowOnDisabled` dropped, or a blank description taken, each
  fails the rendered rows alone;
- a new key read past the resolver fails `EveryKeyIsOverridable`;
- a dialog's tooltip removed, Close's in one and the details box's in the other, or Copy's made blank,
  fails that dialog's tooltip test alone.

Build with warnings as errors, 658 tests (643 passed, 15 skipped as before), the trimmed publish (3
expected warnings, none new) and `check`, which conforms, pass on Windows. Checked again
independently, on top of `plans/00004`: 734 tests (719 passed, 15 skipped), and four canaries of the
checker's own, each red, in the markup, the converter and a dialog. Package validation reports
no breaking change in the three ScopedEditors packages against `2026.4.1110`, nor in AppServices'
`.Avalonia` against `2026.4.1113`, and reports a break planted in ScopedEditors' `.Avalonia` as
`CP0002`. `docs/scopededitors.md` gains "Upgrading from 2026.4.1110". **`AvaloniaDialogService`'s
dialogs are left out** (decision 6): their eleven buttons and their `TextBox` have no tooltip, and no
test could build them then, for their names either; `plans/00007` takes them in. Both families released it at
`2026.4.1116` on the maintainer's word of 2026-11-16, and Templates was told the version that day.

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
JanusMael/Bennewitz.Ninja.Inspections#139, merged on 2026-11-13. `plans/00004`'s headless-dispatch hang
went the same way: Inspections reproduced it on Windows 11 and WSL 2 and put it in
`docs/avalonia-gotchas.md` (JanusMael/Bennewitz.Ninja.Inspections#148, 2026-11-19), and CodeQuality's
`BNCQ3002`, merged in JanusMael/Bennewitz.Ninja.Inspections#149 and not yet released, reports such a wait:
over this repository it reports the wait at `89307bf`, before the fix, and nothing at `3a25858`.

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

**`plans/00004`, a smoke run that fails on a task exception nobody observed, shipped in AppServices
`2026.4.1116`** (approved 2026-11-16; its steps 1 to 6, see Published). The maintainer chose it on
2026-11-13, through `choices` in the Templates session: a smoke run proves a build by its exit code,
and a background failure no collection found before the run ended never reached it.
`AvaloniaDiagnosticsOptions.FailOnUnobservedTaskExceptions`, off by default, has `InstallAvaloniaHooks()`
subscribe a handler to the lifetime's `Exit`. There `GC.Collect` and `GC.WaitForPendingFinalizers` run
twice, and an exit code of 0 becomes 1 when `EntryPointOptions()`' callback has counted, from the
start of the run and whatever `ShowUnobservedTaskExceptions` says, a task exception nobody observed
that is not benign. Avalonia's second report and a benign exception are not counted, a code the app
set is kept, and a lifetime without `Exit` gets a Warning. `AppMain` is unchanged.
`tests/DesktopProbe`, an Avalonia app on the headless platform run as a child process, proves
Avalonia's own wiring: `StartWithClassicDesktopLifetime` runs there, and with the switch on, a task
abandoned once its window opened, and found only by the handler's collection, makes the run exit 1
with the exception on stderr; with it off, a reported one leaves 0. The handlers took 4.4 to 5.3 ms
at exit in a run with nothing to report, 48.8 to 58.6 ms in one whose exception that collection
found and reported, and 0.1 to 0.2 ms with the switch off (five runs each, Windows, Release,
2026-11-16). Build with warnings as errors, 725 tests (710 passed, 15 skipped as before), and
`check`, which conforms, pass on Windows, in the local order and in CI's (see Drift). Each of 13
planted breaks turned the tests aimed at it red.
Package validation against `2026.4.1113` reports no breaking change in `.EntryPoint` or `.Avalonia`,
and reports a planted one (`CP0002`). `docs/appservices.md` gains "Upgrading from 2026.4.1113".
Templates took it in JanusMael/Bennewitz.Ninja.Templates#103 and released it in its `2026.4.1116`:
`bbavalonia` sets `FailOnUnobservedTaskExceptions = App.Smoke` beside its other two switches. On a
generated app run with `--smoke`, a task exception nobody observed exits 1, a cancellation exits 0, and
so does the first with the line set to false.

**`plans/00007` and `plans/00008` are approved** (2026-11-17), to be built as one body of work and
released together, every breaking change in one release, by the maintainer's word: "if we are gonna
break things lets do them all at once".
- `plans/00007` makes `AvaloniaDialogService`'s six dialogs window classes a test builds. It then gives
  every window the package builds names, tooltips, automation ids, and colours from `AppServices.*`
  keys for Light and Dark, and renames the code pane's keys to `AppServices.CodeBlock*`.
- `plans/00008` lets a host replace every string those windows show, through `DialogStrings.Resolver`.
  `IDialogService.ShowConfirmAsync`'s labels default to null, meaning the host's words, and
  `FatalErrorDialog.DefaultMessage` goes.
- `plans/00006`, the theme switch, waits for both, and is revised to build on them.

**`plans/00007` step 2: the six dialogs are window classes a test builds.**
- **The classes.** `AlertDialog`, `MessageAlertDialog`, `ConfirmDialog`, `MessageConfirmDialog`,
  `InputDialog` and `UnsavedChangesDialog` are internal and sealed, in `Dialogs/`. Each builds what its
  method built and exposes its answer as `Result`. The helpers that render a `DialogMessage` and a
  category's header moved with them, to `DialogMessageContent` and `DialogChrome`. A script that compares
  the old file's lines with the new files' accounts for every line the move changed: initializers become
  statements, locals become properties, calls take their class's name.
- **The seam.** Each method of `AvaloniaDialogService` asks `FindOwner` for the window to show over. With
  none it returns its answer for no window; otherwise it shows its dialog over that window with
  `ShowDialog` and returns `Result`. `FindOwner` is the main window of the running application's desktop
  lifetime, unless a test replaces it.
- **The tests.** `DialogWindowTests` drives each dialog alone: each button, Enter and Escape where they
  are wired, and a close without a button. `AvaloniaDialogServiceTests` drives each method end to end
  over a shown window. With no owner, each method, the pickers included, answers at once and opens no
  window.
- **Seen red.** 36 of 38 planted breaks turned the tests aimed at them red, one at a time, each run
  against the two classes. The two that survived are under Drift: the input box's own Enter branch,
  which its OK button's default repeats, and the seam's default reading the running application.
- **Unchanged surface.** Package validation against `2026.4.1116` reports no breaking change in
  `.Avalonia`, and reports a public member planted internal as `CP0002`. The Avalonia package's public
  surface, listed by reflection before and after, is the same 65 lines, but for the build stamp
  AutoVersioning writes into every build.
- **Verified on Windows.** Build with warnings as errors, 806 tests (791 passed, 15 skipped as before),
  and `check`, which conforms.

**`plans/00007` step 3: names, tooltips and ids, and the 📋 and ↗ buttons.**
- **Every interactive control in the eight windows** has a name its automation peer reports, a tooltip
  and an automation id unique in its window: the six dialogs, and `FatalErrorDialog` and
  `NonFatalNoticeDialog`, which gain ids. An id is `Dialog` and the control's role, from
  `DialogAutomationIds`, and a role a message repeats is numbered from its second, `DialogCopyPath2`. The
  input box is named by its prompt. The new tooltips are `DialogTips`, in `plans/00008`'s defaults.
- **The 📋 after a path and the ↗ after a link are buttons** in their text block's line. Each has a
  transparent fill, no border, no padding, no minimum size and the text's weight. The styles the message
  renderer adds to its window keep a theme from filling one pointed at or pressed. Tab reaches each,
  Enter and Space press it, and UI Automation invokes it. A ↗ hands its link to
  `DialogMessageContent.OpenLink`, which a test replaces.
- **The tests.** `DialogAccessibilityTests` walks every window in `DialogWindows`, the tests' table of
  windows, reading each name from the control's peer: a name with no symbol in one the package gives, a
  tooltip, and an id unique in its window, each window's ids pinned. A window class the package declares
  that the table lacks fails. `DialogGlyphButtonTests` runs in each window that shows a message. There a
  click through the headless mouse, Enter, Space and UI Automation's invoke each make a 📋 copy its path
  and a ↗ hand its link to the opener. Tab reaches each, two clicks select no text, and a 📋 says
  "Copied!" and keeps its name. Enter on a focused 📋 copies and leaves a confirmation open. Under Semi,
  which `AppServices.Tests` now references and a test adds to its own application, each glyph button is
  as big as its glyph, no taller than a line of its text and drawn at the text's weight. It stays
  unfilled pointed at and pressed, by the mouse or by Space.
- **Measured first, as the step asks**, on the headless platform under Semi and with no theme:
  - a button's and a text box's peers report their automation name;
  - a selectable text block's peer reports its text, U+FFFC where each inline control sits, and ignores
    an automation name;
  - an `InlineUIContainer`'s child is in its text block's logical tree, shown or not;
  - Tab reaches a button in a text block's line with nothing set;
  - a click on a glyph button selects no text, nor does a press dragged off it.
- **Seen red.** Each of 30 planted breaks turned the tests aimed at it red, one at a time, each run
  against the two classes:
  - a name, a tooltip or an id removed: a glyph's name, the input's, a message's tooltip, an alert's OK,
    Don't Save's id and the fatal dialog's Close;
  - a symbol in a name: 📋 before a copy button's words, ↗ after an open button's, and 📋 kept in the
    notice's Copy to Clipboard;
  - an id repeated, by hand or by a role left unnumbered, and the code pane's text given the text's id;
  - a glyph made a text block again, its press let through to its text block, no tab stop, a null fill,
    and the theme's minimum height, padding, border or weight left on it;
  - the styles that clear the fills removed, both, the pressed one alone and the pointed-at one alone;
    the fills cleared to null; the unsaved-changes prompt left without them;
  - a copy that copies nothing, an open that hands the opener nothing, a 📋 that never says "Copied!"
    and one renamed by it;
  - a window class left out of the table.

  Two survived the first run, and each led to a check:
  - the theme's padding, which Semi gives a button on its left and right alone, so that no height shows
    it, now fails a test that a glyph button is as big as its glyph;
  - the pressed style removed alone, since a press by the mouse is pointed at as well, now fails a press
    by Space.
- **Unchanged surface.** The Avalonia package's public surface, listed by reflection, is the same 65
  lines as step 2's but for the build stamp: the ids, the tooltips and the opener are internal.
- **Verified on Windows.** Build with warnings as errors, 873 tests (858 passed, 15 skipped as before),
  and `check`, which conforms.

**`plans/00007` step 4: the dictionary, its keys, the destructive states and the merge.**
- **The keys.** `DialogColors`, internal, is a resource dictionary whose Light, Dark and Default theme
  dictionaries declare the ten `AppServices.*` keys with the plan's values, the Default holding the Light
  ones. Every colour a window shows is read through `DialogColors.Brush`, a dynamic resource, so an open
  window follows a change of theme. The unreachable `#888888` went, and step 3's transparent fill is
  `DialogColors.NoFill`, so nothing outside the dictionary's type makes a colour or a brush.
- **The breaking change.** The code pane reads `AppServices.CodeBlockBackgroundBrush` and
  `AppServices.CodeBlockBorderBrush`, and no longer `AppCodeBlock*`. The commit is marked `!`, and
  `docs/appservices.md` gains "Dialog colours", the keys and how a host overrides one, and "Upgrading
  from 2026.4.1116", which also names the colours in Dark and step 3's buttons.
- **The destructive states.** A destructive button takes its fill and label from its keys. Two styles
  `DialogChrome` adds to its window paint the button's `PART_ContentPresenter`, fill and label, pointed at
  and pressed, the pointed-at style first, so a press by the mouse, which is pointed at too, takes the
  pressed fill.
- **The merge.** `DialogColors.Brush`'s first call in an application inserts a new instance at index 0 of
  the application's merged dictionaries, unless the merged dictionaries hold one already. A window that
  reads no key merges nothing: `ConfirmDialog`, `InputDialog`, `FatalErrorDialog`, `NonFatalNoticeDialog`,
  and an `AlertDialog` that is not an error.
- **The tests.** `DialogColorTests`, 53:
  - each key declared for Light, Dark and Default, with the plan's value, and nothing else declared;
  - each measured colour against its stand-in window, and the destructive label on each of its fills in
    both themes;
  - each window that reads a key, shown, takes each colour from the package's dictionary in Light, Dark
    and Light again, and the Light colours under a theme variant that inherits neither; with a host's
    colours, which differ between the themes for every key, it follows each change, a destructive
    button's pointed at and pressed included;
  - under Semi, in Light and in Dark, a destructive button filled from its keys at rest, pointed at,
    pressed by the mouse and pressed by Space, its label white throughout, and under a style that paints
    a button's label as Fluent's does, its label still white;
  - a host's keys win, declared among the application's own resources, or in a dictionary it merges
    before the first dialog or once one is open;
  - building each window that reads a key twice merges one dictionary, at index 0; a second application
    gets an instance of its own; a window that reads no key merges nothing; and every window in
    `DialogWindows` has its row in the tests' table of colours.
- **Seen red.** Each of 25 planted breaks turned the tests aimed at it red, one at a time, each run
  against the class:
  - in the dictionary: a value changed, a key missing from Dark, the Default holding Dark's values or
    missing, and a Dark link and a destructive label below their contrast;
  - in the merge: one instance shared between applications, the merge remembered by a flag, the
    dictionary merged last, a new one for every key read, and none merged at all;
  - in the windows: the header glyphs' keys swapped, the alert's glyph given the warning colour, a link's
    literal colour back, the code pane reading the old key, the 📋 given the link colour, a path's colour
    dropped, and the fatal dialog reading a key;
  - in the destructive states: either style removed, the two in the wrong order, the label left to the
    theme, and a fill read once and never followed, at rest or in a state;
  - a window left out of the tests' table of colours.

  As designed, two were each caught by one test alone: the label left to the theme by the style that
  paints a button's label as Fluent's does, and a state's fill read once by the host's colours. The test
  of a theme variant that inherits neither Light nor Dark came after that run: the Default dictionary
  given Dark's values, and removed, each planted again, turned it red in every window.
- **Unchanged surface.** The Avalonia package's public surface, listed by reflection, is the same as step
  3's but for the build stamp: the dictionary, its keys and its helper are internal.
- **Verified on Windows.** Build with warnings as errors, 926 tests (911 passed, 15 skipped as before),
  and `check`, which conforms.

**`plans/00008` step 2: `DialogStrings`, ahead of `plans/00007` step 5.**
- **The strings.** `DialogStrings`, public and static in the Avalonia package's `Localization/`, holds
  `plans/00008`'s 34 keys, each a property named for its key, read through `Resolver` when its window is
  built. Every string the eight windows show that no caller passes is one of them, and so are the
  pickers' titles when a caller passes none, the fatal dialogs' titles and message, and a confirmation's
  labels when its caller passes none. Step 3's `DialogTips` went into it. A string that carries a value
  takes it as `{0}`, put in with the current culture.
- **The two breaking changes**, marked `!`. `IDialogService.ShowConfirmAsync`'s labels default to null,
  which the service shows as `LabelConfirm` and `LabelCancel`, and `FatalErrorDialog.DefaultMessage` is
  gone, `TextFatalError` in its place. `docs/appservices.md` gains "Dialog strings" and three lines under
  "Upgrading from 2026.4.1116".
- **A host's strings that fail.** A resolver that throws, or gives null or blank text, or a text whose
  `{0}` cannot be filled, gives that string its default, and the first failure of each key is logged at
  Warning, through a write that swallows a logger's throw. A key given back gives its default, unlogged.
- **The tests.** `DialogStringsTests`, 29:
  - the walk: each window in `DialogWindows`, built under a resolver that marks every key and with each
    📋 pressed, shows no word but the host's and its caller's, and shows the keys `KeysShown` pins for it;
    every window has its row, and every key is shown in a window or held by a test of its own;
  - outside a window: each picker's title, the fatal dialogs' titles before and after the application is
    named, their message, and a confirmation's labels through the service and through the contract, with
    labels and without;
  - the failures: each of decision 4's, one Warning for each key, none for a key given back, and the fatal
    dialog built when the logger throws as well;
  - every key with a property and `plans/00008`'s default, each property asking for its own name; no
    default naming a product; and a change of the host's words reaching the next window, while an open
    one keeps its own, a 📋's "Copied!" included.

  `DialogWindows` names each string its rows pass, and lists them as `Given`.
- **Seen red.** Each of 33 planted breaks turned the tests aimed at it red, one at a time, each run
  against the class:
  - in a window: the alert's OK, the notice's close tooltip, the fatal dialog's details help, a 📋's
    "Copied!" and the input dialog's Cancel left English, and a code pane's tooltip read from the text's
    key;
  - outside one: the folder picker's title left English, and the save picker's title given ignored; the
    fatal dialog's title, the native one's before and after the application is named, the handler's
    message and `ShowFatalErrorDialog`'s left English, and a message given ignored; the contract's and the
    service's confirm label defaulting to "Confirm", the cancel label for none left English, and a label
    given ignored;
  - the failures: a resolver's throw let through, null taken for blank, blank text shown, every failure
    logged, a key given back logged or shown, a placeholder's `FormatException` let through, a logger's
    throw let through, and `ResetForTesting` forgetting no failure;
  - the keys: a default changed, a property reading another key, a default naming a product, a string read
    once and kept, the 📋's name filled from the link's key, and "Copied!" read when the 📋 is pressed
    rather than when its window is built.
- **The surface.** Package validation against `2026.4.1116` reports `CP0002` for the removed
  `FatalErrorDialog.DefaultMessage` in `.Avalonia`, and nothing in `.Abstractions`, since it cannot see a
  parameter's default or its nullability; the commit is marked `!` for both. The Avalonia package's public
  surface, listed by reflection, gains `DialogStrings` and loses `DefaultMessage`, and the service's two
  `ShowConfirmAsync` take null labels by default; the rest is step 4's but for the build stamp.
- **Verified on Windows.** Build with warnings as errors, 955 tests (940 passed, 15 skipped as before),
  and `check`, which conforms.

**Next, in order:**
1. **AppKit takes Templates' `reference/src` layout**, in a pull request of its own: Templates released it
   in `2026.4.1119` (above).
2. **`plans/00007` step 5**, once AssemblyQuality releases its IL reader: raise its pin, build the sweeps on
   it, and add `src/AGENTS.md`'s rule for windows. The Inspections session plans the reader, its number and
   its order the maintainer's to set there, and tells this repository its version and API on release.
3. **`plans/00007` step 6 and `plans/00008` step 3**, the docs: the root `AGENTS.md`'s invariants on the
   windows' colours and strings, and what `docs/appservices.md` still lacks.
4. **AppServices' next release carries both plans**, on the maintainer's word. The day it is published,
   ClaudeForge's session is told and takes it at once (`plans/00007` step 8, `plans/00008` step 5), and
   XamlQuality gets the Avalonia findings.
5. **`plans/00006`** is revised to build on both, then put to the maintainer.
6. **ClaudeForge's change is its own** (`plans/00002` step 6 and `plans/00003` step 5, told on
   2026-11-12 and 2026-11-13): in one commit, take `2026.4.1113`, set its classifier as
   `IsBenignException`, and delete its UI-thread handler, its unobserved handler and its rebuilt
   options; it needs no unobserved handler of its own. OpenForge2k's copy of ClaudeForge holds
   `2026.3.924` until it can make the same change.
7. **ScopedEditors' next release** is a family release, `scopededitors/v…`, on the maintainer's word
   when the family next changes. The policy names this repository and covers all eight ids, each pushed
   at `2026.4.1116`.

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
- **The 120 s smoke hang in the plan's "What exists" was most likely not the dialog.** `bbavalonia`
  ends a smoke run by posting `desktop.Shutdown(0)` from an `Opened` handler that `App` subscribes
  after the window is built, and an explicit shutdown ends a run whatever windows are open; the
  `FatalErrorDialog` is modeless, so it never stops that shutdown from running. A throw from the
  window's own handler most likely ended the event before `App`'s ran (read in
  JanusMael/Bennewitz.Ninja.Templates#97's `App.axaml.cs`, not measured). Templates' canary of
  2026-11-13, an unobserved exception with the switch forced on, exited 0 in 3 s.
  `docs/appservices.md` said an open dialog keeps a smoke run alive; it now says which runs one holds
  open.

## Drift from `plans/00004`

- **More is tested than the plan names.** Every row of the shared table runs with
  `ShowUnobservedTaskExceptions` on and off, which decision 2's "whatever it says" asks for. A test
  proves the second round of collection: an object whose finalizer abandons a faulted task, found
  only by that round, and both one round and the request's three steps turn it red. The count is
  shown to run from before `ConfigureLogging` as well as before `InstallAvaloniaHooks()`.
- **The switch subscribes the UI-thread handler too, handling nothing, so that decision 2 holds under
  a smoke run's switches.**
  - **The gap.** With `HandleUiThreadExceptions` and `ShowUnobservedTaskExceptions` both off, nothing
    else remembers what the dispatcher reported. So the second report of an exception an app's own
    handler dealt with was counted, and failed the run. An independent judge of the first commit found
    it, on 2026-11-16.
  - **Now.** `InstallAvaloniaHooks()` installs nothing only with all three switches off.
  - **Guarded.** The second-report test runs every row with the package's handler and with an app's
    own handler under a smoke run's switches; with the subscription reverted, the 8 rows whose
    unobserved verdict is not benign fail.
- **That nothing is hooked with the switch off is shown in process, not by the probe.** The handler
  reads the switch itself, so the probe's run with it off would exit 0 either way. The Warning test
  with the switch off shows it: a hook installed with the switch off turns it red.
- **That the handler is subscribed once is guarded by its code alone.** No test can see a lifetime's
  subscribers without reading Avalonia's private field, and a second subscription would change only
  the pause: the second call finds the code already 1.
- **What the plan leaves open, settled here:**
  - the handler reads the switch itself as well, so a call with it off changes nothing;
  - the count's line is logged at Error, with the count as `UnobservedCount`, and nothing is logged
    when the app's own code is kept;
  - the Warning names the lifetime's type, or `none`;
  - the tests and the probe abandon a task faulted through a `TaskCompletionSource`, never run, so
    that no pool thread still holds it when the handler's collection comes.
- **The probe takes its switches as arguments**, `fail`, `drop` and `collect`, and times the handler
  between two `Exit` handlers of its own, subscribed before `InstallAvaloniaHooks()` and after it.
- **CI's order hung the first run** (2026-11-16, run `43912835872`), where the local order passed:
  - **What blocked.** The second-report test blocked on `Session.Dispatch` right after an async
    headless test.
  - **Why.** Avalonia 12.1.3's headless session completes a dispatch's task on its own consumer
    thread, without `RunContinuationsAsynchronously`, so that test ran on the session's thread and
    waited for a dispatch only that thread could run.
  - **How it was found.** In a worktree at the Windows runner's path, where xUnit orders the tests as
    CI does, read with `dotnet-stack`.
  - **The fix.**
    - The two tests that need the UI thread await `AppMain.RunHostAsync` now.
    - The class suppresses the native fatal dialog.
    - Both run helpers fail when the work threw.
  - **Recorded.** `tests/AGENTS.md` has both rules, and the finding went to the XamlQuality session for
    `docs/avalonia-gotchas.md`.
  - **Proved.** In the same order AppServices' tests then passed, none failing: 439 at that fix, and
    455 with the change above.

## Drift from `plans/00005`

- **The `BNXQ1011` count is pinned exactly, at 12.** Decision 2 pins it "as the BNXQ1002 test pins its
  twelve", but that test asserts at least ten, with twelve in its comment. Pinned exactly, a new input
  control raises it, as the decision asks.
- **`Skipped` is asserted before the count.** A control the rule skips is not counted as inspected:
  with a tooltip removed and a style's setter planted, `Inspected` fell to 11, and the count's message
  called it a control the scan stopped seeing. Asserted first, the skipped control is named.
- **What the plan leaves open, settled here:**
  - the converter is `FirstNonBlankConverter`, public beside the package's other converters: a
    `MultiBinding` lists `Description` and then `DisplayName`, and a description that is empty or white
    space counts as none;
  - `BNXQ1011` runs as Templates runs it, without the four element names the naming and id tests add;
    none occurs in this markup;
  - the four keys read "Pick the path instead of typing it", "Remove this item from the list", "Type an
    item to add to the list" and "Add the typed item to the list";
  - the four fixed controls do not set `ShowOnDisabled`, which decision 3 asks of the value inputs
    alone, so on a locked row, where they are disabled, their tips do not show;
  - the rendered rows take a blank description as well as a missing one, and assert that a locked
    row's input is disabled, so `ShowOnDisabled` is read where it matters;
  - in the two dialogs, Copy's and the details box's tooltips are each control's help text, kept as
    one string, and Close's, which has no help text, reads "Close this window";
  - the dialogs' tooltip check is a test of its own for each dialog, beside the naming test, so a
    missing tooltip and a missing name fail apart; it asserts the walker's count first, and takes a
    tooltip that is not a string as one.
- **`tests/AGENTS.md`'s ScopedEditors section** also said its project references all of `src/`; it
  names the family's three projects now.

## Drift from `plans/00007`

- **A button has a template on the headless platform with no theme loaded.** The plan's "What exists"
  says a test's button has none. Avalonia 12.1.3 gives every `ContentControl`, a window and a button
  among them, a default template holding a `PART_ContentPresenter` (`ContentControl.cs`, 47 to 63).
  So a shown window puts its content in the visual tree, and a button's `IsDefault` and `IsCancel`
  hear Enter and Escape on it with no theme. A text box has no template and still takes focus and typed
  text (measured 2026-11-19). Step 2's tests run on `HeadlessTestApp` as it is, and `Semi.Avalonia`
  joins the test project with step 3, which measures under it.
- **Measured first, as step 2 asks.** On the headless platform, a dialog shown with `ShowDialog` over a
  shown window is listed among that window's `OwnedWindows` and reports `IsDialog` while it is open, and
  the task `ShowDialog` returns completes as it closes. So the owner seam does not take the show step,
  and every method is driven end to end. Enter on such a dialog presses its default button, never the
  owner's, though the owner's had focus.
- **"No window built" is tested as no window opened.** A class handler on `Window.WindowOpenedEvent`
  sees every window shown. Nothing a test can read sees a window constructed and never shown, short of
  replacing the windowing platform.
- **The input box answers Enter twice.** With focus in the box, Enter runs the box's own handler, which
  takes the trimmed text and closes the window, and then the OK button's default, which does the same.
  So a break planted in the box's Enter branch survives, and one in OK's handler fails the Enter test
  too. Escape runs the box's branch and then the cancel button, which closes with no answer: only a
  break that gives Escape an answer is seen. Step 2 moves code without changing it, so both branches
  stay.
- **The seam's default is guarded by its code alone where it reads the running application.**
  `DesktopMainWindow`, which takes the application, is tested with one that was never set up. The
  headless application's lifetime cannot be set once it is set up, and `AvaloniaLocator`, which would
  swap the application, is Avalonia's private API. So a planted default that finds nothing survives.
- **What the plan leaves open, settled here:**
  - the seam is `FindOwner`, a `Func<Window?>` on each service, named as `UiThreadExceptions.OpenDialog`
    is; the pickers and the Save Changes factory ask it too, since they read the same main window
    before;
  - `icon ?? DialogAppIcon` stays in the service, and each class takes the icon it shows;
  - each class exposes the controls a test drives as internal properties, and its answer as `Result`;
  - the two helper classes are internal and static, their bodies unchanged;
  - the tests press a button through its automation peer's invoke, as UI Automation presses it.
- **The 📋 and ↗ have a transparent fill, where decision 5 says no background.** On the headless
  platform the mouse finds only what paints a fill. Text takes no pointer and neither does a null fill,
  so neither today's text glyph nor a button with no fill takes a click there; on a screen a null fill
  takes the pointer only where the glyph is drawn. A transparent fill takes it over the whole button and
  paints nothing (measured 2026-11-19).
- **The window's styles clear a theme's fills to transparent, not to null.** Cleared to null, a glyph
  pointed at no longer takes the pointer, so the press that follows never reaches it: planted, that
  turned every click red.
- **Tab reaches a glyph with nothing set.** A text block's tab navigation is `Continue` by default and
  its inline controls are its visual children, so Tab goes from the block, a tab stop of its own as
  before, to each glyph in its line, under Semi and with no theme (measured). The step's "where its text
  block's tab navigation is otherwise set" did not arise.
- **"No taller than its line" is read against a line of the same text block that holds no glyph.** A
  line grows to hold what is in it, so a glyph is never taller than its own. Under Semi on the headless
  platform a glyph is 14 high and a line of text 15.3; a line that holds glyphs is 18.1, the same as one
  that holds today's text glyphs (measured).
- **"The click selects no text" is tested with two clicks.** One click never selects text, and on the
  headless platform a drag across text selects nothing either, since text takes no pointer. With a
  glyph's press let through to its text block, planted, two clicks select the glyph's own U+FFFC.
- **The glyph is drawn at the text's weight**, which the plan does not name: Semi draws a button's label
  at 600, and the text glyph took the text's. A test holds it.
- **What the plan leaves open for step 3, settled here:**
  - the ids are `DialogAutomationIds`' constants, numbered by one `Sequence` for each message;
  - the new tooltips are `DialogTips`' constants until `plans/00008` step 2 routes them through
    `DialogStrings`;
  - the input box is named through `AutomationProperties.Name`;
  - the opener is a static `DialogMessageContent.OpenLink` with `ResetForTests`, as
    `UiThreadExceptions.OpenDialog` is, and its default, the platform shell, is guarded by its code
    alone, since a test that ran it would open a browser;
  - the message renderer adds the fill styles to the window it renders into, so no window that shows a
    message lacks them;
  - the walker still walks each window unshown, and the table of windows is `DialogWindows`, which
    `plans/00008`'s marker walk reads.
- **The transparent fill comes from the dictionary's type**, as `DialogColors.NoFill`, since step 4.
  Step 3 read `Brushes.Transparent` in a glyph's fill and in the styles, which decision 8's sweep forbids
  outside that type.
- **A destructive button's label is painted pointed at and pressed, beside its fill.** Decision 6 names
  the presenter's fill. Fluent's button paints its presenter's label as well in both states (its
  `Button.xaml`, 48 and 58), dark on the destructive red. `AppServices.Tests` references no Fluent, so a
  test adds a style that paints a button's label as Fluent's does; with the label's setter removed, that
  test alone turned red, since Semi leaves the label alone.
- **That a destructive button's colours follow a change of theme is shown with a host's colours.** The
  package gives its four keys one value in both themes, so a fill read once, never followed, passes every
  test of the package's own values. A host's colours differ between the themes for every key, and a fill
  read once turned that test red.
- **`docs/appservices.md` changed with step 4, ahead of step 6.** The root `AGENTS.md` gives a breaking
  change its line in the family's README in the commit that makes it, so "Dialog colours" and "Upgrading
  from 2026.4.1116" came with the keys' rename; step 6 adds what is left.
- **More is tested than the plan names:** a press by Space as well as the mouse; each window under a theme
  variant that inherits neither Light nor Dark, which decision 6 gives the Default dictionary for; and
  every window that reads no key merging nothing, the internal ones among them.
- **What the plan leaves open for step 4, settled here:**
  - the dictionary is `DialogColors`, internal and sealed, a `ResourceDictionary` holding the keys as
    constants, the helper `Brush` and `NoFill`; its brushes are immutable;
  - the helper merges on its first call in an application, so the first window that reads a key merges,
    while it is built; with no application it merges nothing;
  - a destructive button carries the class `dialog-destructive`, which its window's two styles select;
  - the stand-in windows and the thresholds are the tests' constants, and the contrast is WCAG 2's.
- **Step 5 waits for an IL reader in AssemblyQuality**, by the maintainer's choice of 2026-11-19. Decision 8
  reads the sweeps from the package's IL, and AssemblyQuality had no IL reader. His words, of
  AssemblyQuality: "we should make that have an IL reader if it does not already have one", and "I would
  like to include the most feature rich, rather than the lightest in that case". He chose
  ICSharpCode.Decompiler, over Mono.Cecil and dnlib, and the Inspections session, which keeps
  AssemblyQuality, to plan it there for his approval, with step 5's four cases as its first consumer:
  windows, colours, lookups and keys. Step 5 raises the pin and builds the sweeps on it once it is released,
  and `plans/00008` step 2 came first.

## Drift from `plans/00008`

- **Step 2 came before `plans/00007` step 5**, which the plan puts it on top of, by the maintainer's choice
  of 2026-11-19: step 5 waits for an IL reader in AssemblyQuality (Drift from `plans/00007`).
- **Measured first, as step 2 asks: a test cannot read a picker's options from the headless platform.**
  - **What the platform gives.** On Avalonia 12.1.3's headless platform a window's storage provider is
    `NoopStorageProvider`. It cannot pick a folder, and it answers every picker at once with nothing,
    whatever its options (measured 2026-11-19).
  - **Why a test cannot replace it.** `TopLevel` takes a provider from `AvaloniaLocator`, which is
    Avalonia's private API, or from the platform, and `IStorageProvider` is `[NotClientImplementable]`.
  - **So.** `AvaloniaDialogService` builds each picker's options in an internal step, which its test reads.
    That the method opens its picker with them is guarded by its code alone.
- **`docs/appservices.md` changed with step 2, ahead of step 3**, as it did with `plans/00007` step 4: the
  root `AGENTS.md` gives a breaking change its line in the family's README in the commit that makes it.
- **More is tested than the plan names:**
  - each window's keys are pinned, so a string read from the wrong key fails as a string left English does;
  - every key is shown in a window of the table or held by a test of its own;
  - a confirmation's labels are read through the contract as well as the service, since a call compiles
    the contract's defaults into itself;
  - decision 2 is tested: a change of the host's words reaches the next window, and an open one keeps its
    own, a 📋's "Copied!" included.
- **What the plan leaves open, settled here:**
  - `DialogStrings` is in the namespace `Bennewitz.Ninja.AppServices.AvaloniaUI.Localization`;
  - a string that carries a value is filled by an internal method beside its property, such as
    `TipCopyPathFor`;
  - the Warning reads "DialogStrings.Resolver {Failure} for {DialogStringKey}, so its default is shown",
    its failure "threw", "gave null", "gave blank text" or "gave text whose placeholder cannot be filled";
  - a window reads each string once, so a help text and the tooltip that repeats it are one string, and a
    📋's "Copied!" is read when its window is built;
  - step 3's `DialogTips` went into `DialogStrings`, and `DialogWindows` names each string its rows pass,
    listed as `Given`.

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
| `2026.4.1116` | `appservices/v2026.4.1116` | `plans/00004` and `plans/00005`; release run `43931579532`. All five ids listed by the nuget.org flat-container with a nuspec naming AppKit at `31e9c7a`, and a project with nuget.org as its only source restored them |
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
| `2026.4.1116` | `scopededitors/v2026.4.1116` | `plans/00005`; release run `43931579797`. All three ids listed by the nuget.org flat-container with a nuspec naming AppKit at `31e9c7a`, and a project with nuget.org as its only source restored them |
| `2026.4.1110` | `scopededitors/v2026.4.1110` | First from AppKit; release run `42842847901`. All three ids listed by the nuget.org flat-container with a nuspec naming AppKit at `a1f42db`, and a project with nuget.org as its only source restored them |
| `2026.4.1103` | `scopededitors/v2026.4.1103` | All three ids listed by the nuget.org flat-container, and a project with nuget.org as its only source restored them |
| `2026.4.1031` | `scopededitors/v2026.4.1031` | Listed; release run `41231920267` |
| `2026.3.924` | `scopededitors/v2026.3.924` | Listed. OpenForge2k consumes it from nuget.org |
| `2026.3.923` | `scopededitors/v2026.3.923` | Listed |

## Next, by family

**AppServices**
1. **Headless isolation per assembly** is unverified and waits for evidence before
   `[assembly: AvaloniaTestIsolation(AvaloniaTestIsolationLevel.PerAssembly)]` is considered. Some
   arrived on 2026-11-16, from ClaudeForge's session through Templates, measured on Avalonia 12.1.6:
   - **Under per-test isolation, a test can fail at set-up.** Every dispatch sets the session's
     application up again. A background thread that reads `Dispatcher.UIThread` in that window, from a
     leftover continuation or a file watcher, becomes the UI thread, and the set-up throws "a different
     thread owns it". The failing test is a victim.
   - **Per-assembly isolation with one warm-up dispatch** stopped it under a stress test.
   - **It is no drop-in.** It failed three of ClaudeForge's tests outright where the assembly also holds
     tests that are not headless, since one application then exists for the whole process, and queued
     dispatcher work leaked between tests. Both of this repository's test assemblies hold such tests.
   - The finder's verdict waits on its CI. These tests already keep no warm-up (`tests/AGENTS.md`).
2. `LiveLogWindowSink` and its windows stay in OpenForge2k until they can ship together; nothing here
   is scheduled for them.

**ScopedEditors**
1. **Per-assembly headless isolation is unverified**, as above. It is the candidate fix for the
   cross-thread failure seen on OpenForge2k's CI, and OpenForge2k has not reproduced that failure since
   its xUnit move (measured 2026-09-25) and parked its own attempt because it broke a seam. The
   evidence of 2026-11-16 is under AppServices' item: per-assembly isolation is no drop-in for an
   assembly that also holds tests that are not headless, as this one does.

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
