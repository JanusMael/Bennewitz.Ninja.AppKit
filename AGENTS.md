# AGENTS.md — Bennewitz.Ninja.AppKit

> For anyone changing this repository, human or agent. This top half is the repository as a whole:
> the two families it holds, how each releases, the invariants they share, the commands and the
> checklists. Each family's own contract follows under its name. Each top-level directory has an
> `AGENTS.md` of its own for what only its files show.
>
> Work state lives in [`PROGRESS.md`](./PROGRESS.md), updated in the same change as the work it
> describes. What every repository in this family carries, and how it is checked, is prescribed in
> [`docs/repository-conventions.md`](https://github.com/JanusMael/Bennewitz.Ninja.Templates/blob/main/docs/repository-conventions.md)
> in Bennewitz.Ninja.Templates, whose `plans/00009` brought the two families here.

## What this repository is

Building blocks for .NET desktop apps, in two families, each with its own packages, version line
and releases.

| Family | What it is | Packages | Projects | Its README |
|---|---|---|---|---|
| `appservices` | Application services behind contracts a test can fake, and one entry point for every kind of app | `Bennewitz.Ninja.AppServices`, `.Abstractions`, `.Logging`, `.Avalonia`, `.EntryPoint` | `src/AppServices*` | [`docs/appservices.md`](docs/appservices.md) |
| `scopededitors` | Schema-driven property editors for data set at more than one scope | `Bennewitz.Ninja.ScopedEditors.Abstractions`, `.ViewModels`, `.Avalonia` | `src/ScopedEditors*` | [`docs/scopededitors.md`](docs/scopededitors.md) |

Each family's README is its packages' nuget.org page: each package packs its family's file as
`README.md` (`FamilyReadme` in its csproj). The root [`README.md`](README.md) is the overview.

The repository grew from Bennewitz.Ninja.AppServices, renamed; ScopedEditors' history was imported
whole, its tags renamed `scopededitors/v…`. DiffView stays in Bennewitz.Ninja.DiffView.

## Layout

| Path | What it holds |
|---|---|
| `src/` | The shipped projects, each folder named for its family |
| `tests/` | The families' test projects, and the apps AppServices' entry-point tests run |
| `trimcheck/` | ScopedEditors' rooted trimmed publish and its ILLink baseline |
| `docs/` | Each family's README and the release runbook |
| `plans/` | Numbered plans, continuing AppServices' sequence |
| `scripts/` | File-based apps: the release resolution, the packaging guard, the conventions check, and each family's own tools |
| `reference/` | Read-only checkouts of the upstream sources at the versions the build pins, one folder each in `reference/src/`, which git ignores whole, beside the tracked files that fetch and describe them: `fetch-reference.cs`, its manifest `sources.json`, three MSBuild shields and the README. A search prunes the checkouts unless the task is about one, and reaches one by passing its path |
| `AppKit.slnx` | The one solution: every family's `src` and `tests` projects |
| `AssemblyInfo.InternalsVisibleTo.cs` / `.External.cs`, `Directory.Build.targets` | Templates' solution-wide friend grants: every project grants its internals to every assembly the solution builds |

## Families and releases

Each family releases alone, from its own tag, `<family>/vYYYY.Q.MMDD`: `scopededitors/v2026.4.1201`.
`scripts/release-target.cs` resolves the tag into the family's ids from `.github/repository.json`'s
`families`; the release packs only those projects, publishes only those ids, and writes its notes
from the family's own commits since its previous tag. The bare `v…` tags were AppServices' releases
before the merge (`"bareTags": "appservices"`), so its first family release starts from the last of
them. A bare `v…` tag, or a dispatch without a family, is refused before anything builds. A family is
released when it changes, never because another did.
[`docs/publishing.md`](docs/publishing.md) is the runbook.

⛔ **A packable project never references another family's packable project.** A family's release
packs at that family's version, so the reference would pack a dependency on a version of the other
family that need not exist. Across families, a package references the other's package at a
published version. `repo-conventions check` fails the packable case.

## Invariants every family shares

| Invariant | Failure if broken | Guarded by |
|---|---|---|
| Every packable project's id is in exactly one of `packages.push` and `packages.local`, and the ids of `families` are exactly `packages.push`'s | A package nobody chose is published permanently, or a declared id is never released | Each family's `PackagingTests`; `scripts/assert-packages.cs`; `repo-conventions check` |
| A release publishes the ids `release-target.cs` resolves for its family, each by its exact file, and never globs `*.nupkg` | A package of another family, or a new packable project, is published by the next tag | `.github/workflows/release.yml`; `release-target.cs`; `assert-packages.cs --family`; `repo-conventions check` |
| The version is the tag's, `YYYY.Q.MMDD` with the quarter of its date | The package and the tag disagree. A published version can never be replaced | `release-target.cs`, before anything builds |
| A release's version is never below the newest nuget.org lists for one of its family's ids, unless nuget.org already lists it (a re-run, whose push is skipped) | nuget.org takes the lower version without a word, and it is permanent | `release-target.cs resolve`, which reads nuget.org's flat container and refuses when it cannot |
| `NUGET_USER` is a repository **variable**, not a secret | A masked value hides why a trusted-publishing login fails | `release.yml`, step `Refuse to release without NUGET_USER` |
| Every package id resolves from exactly one source, nuget.org | A second source added later silently starts supplying packages | `NuGet.config`, `packageSourceMapping` |
| Versions are pinned centrally, one pin per package, transitive ones included | Two projects drift to different versions of one dependency | `Directory.Packages.props`, `CentralPackageTransitivePinningEnabled` |
| Warnings are errors | A warning ships | `Directory.Build.props`; `ci.yml` builds with `-warnaserror` |
| Every library is trimmable | A consumer's trimmed publish keeps an unmarked assembly whole | `src/Directory.Build.props`; `repo-conventions check` |
| Every project grants `InternalsVisibleTo` to every assembly this repository builds, from the generated `AssemblyInfo.InternalsVisibleTo.cs`, and to other repositories only from `AssemblyInfo.InternalsVisibleTo.External.cs` | A seam between two projects becomes public API for want of a grant, and a grant from anywhere else goes unseen | `repo-conventions check` |
| The repository meets the family conventions | Documentation or settings go missing unnoticed | `scripts/repo-conventions.cs`, run by CI and by the release |

## Commands

```bash
dotnet build AppKit.slnx -c Release --nologo -warnaserror
dotnet test --solution AppKit.slnx --no-build -c Release
dotnet pack AppKit.slnx -c Release --nologo --output ./packages/Release
dotnet run scripts/assert-packages.cs -- ./packages/Release
dotnet run --file scripts/repo-conventions.cs -- check
dotnet run --file scripts/repo-conventions.cs -- grants
dotnet run --file reference/fetch-reference.cs
```

- Tests run on Microsoft.Testing.Platform (`global.json`), so `dotnet test` takes `--solution` and
  rejects VSTest-only switches such as `--nologo`. `--no-build` needs the Release build first.
- A solution-wide switch reaches every family's tests. `--filter-class` finds nothing in the other
  assemblies and exits 8, so it takes `--project`.
- `repo-conventions.cs` reads GitHub through the `gh` CLI, so a local run needs a `gh` login.
- Write `-p:` rather than `/p:`: Git Bash on Windows rewrites a leading-slash argument into a path.
- `grants` rewrites `AssemblyInfo.InternalsVisibleTo.cs` from every project's assembly name. Run it
  after adding, removing or renaming a project; `check` fails until the list matches.
- `reference/fetch-reference.cs` fetches the upstream sources `reference/sources.json` lists, each at
  the commit its packages' nuspecs record for the pinned version. `-- --status` reports without
  fetching, and `-- --update` fetches again a checkout a raised pin has left behind;
  `reference/README.md` has the rest.
- Each family's own commands are in its section below.

## Checklists every family shares

**Adding a package:** add the project under `src/`, in a folder named for its family; its id to
`packages.push` and to its family in `.github/repository.json`, or to `packages.local` with the
reason; `FamilyReadme` and `AssemblyProduct` in its csproj; confirm the trusted-publishing policy's
pattern for its family covers it (`docs/publishing.md`); the family's own section adds its steps;
record it in `PROGRESS.md`.

**Adding a family:** its stem in `families`, lower-case; its ids in `packages.push`; its README in
`docs/<family>.md`; its tests in a project of their own; a section in this file and in `README.md`.

**Adding, removing or renaming a project:** run `repo-conventions grants`.

**Adding a top-level directory:** give it an `AGENTS.md` and a `CLAUDE.md` containing `@AGENTS.md`,
or exempt it in `.github/repository.json` under `undocumented`, with the reason.

**Changing a dependency version:** in `Directory.Packages.props` only. One pin serves every family, so
a raise moves the floor of every package that references it: say so in that family's README. Moving
Avalonia moves `Avalonia.Headless` with it, and moving `Avalonia.Controls.DataGrid` can move
ScopedEditors' trim baseline.

**Releasing a family:** only on the maintainer's word for that release. `docs/publishing.md`; tag
`<family>/vYYYY.Q.MMDD`, one release per calendar day per family; verify against the feed, then record
it in `PROGRESS.md`.

**Avalonia and drivable-UI lessons go to XamlQuality.** `docs/avalonia-gotchas.md` and
`docs/ai-drivable-ui.md` in
[JanusMael/Bennewitz.Ninja.Inspections](https://github.com/JanusMael/Bennewitz.Ninja.Inspections)
are the one living copy of each. Send a new finding or a correction to the XamlQuality session by
message, with the versions and the measurement or source behind it, or open an issue there when no
session is running. Keep no copy here.

**Every change:** update `PROGRESS.md` in the same commit. Commits are Conventional Commits, with `!`
on a change that breaks a consumer, and a breaking change gets a line in the family's README, which
is what nuget.org shows.

# AppServices

## What AppServices is

Application services for desktop apps — shell launching, environment probing, sharing, dialogs and
logging — behind contracts a test can fake without referencing a UI toolkit. It ships packages to
nuget.org, layered so a consumer takes only what it needs: `Bennewitz.Ninja.AppServices.Abstractions`
(the contracts and the dialog vocabulary, referencing nothing), `Bennewitz.Ninja.AppServices` (default
implementations that need an operating system but no UI framework), `Bennewitz.Ninja.AppServices.Logging`
(Serilog sinks) and `Bennewitz.Ninja.AppServices.Avalonia` (Avalonia dialogs, each a window class a
test builds, with colours for Light and Dark and words a host replaces, `plans/00007` and
`plans/00008`, pickers, the bridge from Avalonia's logger into Serilog, and the handler for exceptions
the UI thread lets escape, `plans/00002`, through which a task exception nobody observed is shown
too, `plans/00003`, and the exit handler with which one fails a smoke run, `plans/00004`). The code
moved here out of OpenForge2k, whose Avalonia applications are the consumers: they reference the
Avalonia package, which brings its siblings through its project references. A test or a non-UI host
references `.Abstractions` or `Bennewitz.Ninja.AppServices` alone.
`Bennewitz.Ninja.AppServices.EntryPoint`, referencing nothing, is the entry point every app's `Main`
calls, console, web and desktop alike (`plans/00001`).
`docs/appservices.md` is packed into every package as its `README.md` and is the nuget.org
description; the root `README.md` is the repository's overview.

## AppServices's invariants

| Invariant | Failure if broken | Guarded by |
|---|---|---|
| `AppServices.Abstractions` references nothing but the framework | A test or non-UI host inherits a UI toolkit or a logger, and the split stops paying for itself | `LayeringTests.Abstractions_compiles_against_nothing_but_the_framework`, `LayeringTests.No_project_reaches_outside_its_tier` |
| `AppServices.EntryPoint` references nothing but the framework | A native AOT app that takes it for its `Main` carries whatever it references | `LayeringTests.EntryPoint_compiles_against_nothing_but_the_framework`, `LayeringTests.No_project_reaches_outside_its_tier` |
| `AppServices.EntryPoint` is native AOT compatible, measured | A native AOT app, such as the family's `bbapi`, fails to publish once it calls `AppMain` | `IsAotCompatible` in its csproj; CI's `aot-linux` and `aot-windows` publish `tests/EntryPointProbe` with native AOT and run `EntryPointProbeTests` against the binary |
| An entry point exits 0 on success, 1 when an exception escapes, 2 on a console usage error and 130 after Ctrl+C, with the fatal report on stderr before any callback | A calling script cannot tell a crash from a bad invocation or an interruption, and a failing logger takes the report with it | `AppMainTests`, `EntryPointProbeTests` |
| The app's log is flushed on every exit, SIGTERM included: a console or desktop run flushes from its SIGTERM registration, and a web run's host ends the run | A stopped service or a closed session loses its log's last events, since on Linux the runtime raises no `ProcessExit` for SIGTERM | `AppMainTests.The_log_is_flushed_once_per_run_however_the_run_ends`, `EntryPointProbeTests.SIGTERM_flushes_the_log_and_the_signal_still_ends_the_process` |
| Every run that reaches its work offers `[App.Start] app=… version=… pid=…` to `AppMainOptions.WriteLogLine` just before it, so one that answers `--version` offers none, and a run that offered it writes one `[App.Exit]` line at the first of its ends, once its code is known and before `FlushLog`: `code=`, `signal=SIGTERM`, `reason=unhandledException` or `reason=hostAborted`, each with `elapsedMs`. `EntryPointOptions()`' writer holds the start line until `ConfigureLogging` has made the log, writes it there first, drops it when its run ends first, and writes the repeat counts still pending before the exit line | A log no longer says when a run began or how it ended, or says it twice; an exit line written after the flush is lost; an Avalonia app, whose log is made inside its run, loses its start line, or a run that never made its log opens a later run's with its line | `AppMainTests.A_run_offers_the_start_line_before_its_work_and_writes_the_exit_line_after_it_and_before_the_flush`, `The_exit_line_carries_the_code_the_run_returns`, `One_exit_line_and_one_flush_however_many_ends_a_run_reaches`, `A_run_that_answers_version_writes_neither_line_and_still_flushes`; `EntryPointProbeTests.SIGTERM_flushes_the_log_and_the_signal_still_ends_the_process`; `EntryPointWebProbeTests.A_stop_on_HostBuilt_passes_its_HostAbortedException_back_with_nothing_on_stderr`; `AvaloniaDiagnosticsEntryPointTests.A_run_that_makes_its_log_in_its_work_opens_it_with_its_start_line_and_ends_it_with_its_exit_line_after_every_repeat_count`, `A_start_line_whose_run_ends_before_its_log_is_made_is_never_written_into_a_later_log` |
| `HostAbortedException` passes through `AppMain` unreported | A design-time tool's normal stop is reported as a crash, and shows a desktop app's fatal dialog | `AppMainTests.A_HostAbortedException_passes_through_unreported`, `EntryPointWebProbeTests.A_stop_on_HostBuilt_passes_its_HostAbortedException_back_with_nothing_on_stderr` |
| A task exception nobody observed that `AppMainOptions.IsBenignException` calls benign stays off stderr and still reaches `OnUnobservedTaskException`; any other is reported, a filter's own throw beside it, and a failure that ends the run is reported whatever the filter says | Known noise floods stderr, a broken filter goes unseen, or a filter hides a crash | `AppMainTests.A_benign_unobserved_exception_leaves_stderr_empty_and_still_reaches_the_callback`, `A_non_benign_unobserved_exception_is_reported_as_before`, `A_throwing_filter_is_reported_along_with_the_exception`, `A_fatal_exception_is_reported_whatever_the_filter_says` |
| The benign rule's two copies, in `AppServices.EntryPoint` and `AppServices.AvaloniaUI`, give every row of one table its verdict | What reaches stderr and what the Avalonia package logs and shows disagree about what is noise | `BenignExceptionRuleTests`, over `BenignExceptionTable`, which every test of the filter reads |
| `AppMainOptions` is a `sealed record`, and stays binary compatible with `2026.4.1110` | A host rebuilds the options by hand to change one callback, and drops a member it forgot; a consumer compiled against the class fails to bind | `AppMainTests.With_changes_one_member_and_keeps_the_rest`; a pack with `EnablePackageValidation` against `2026.4.1110`, run by hand |
| `InstallAvaloniaHooks()` installs the UI-thread handler once, while `HandleUiThreadExceptions`, `ShowUnobservedTaskExceptions` or `FailOnUnobservedTaskExceptions` is set, and not at all with all three false; the handler handles only while `HandleUiThreadExceptions` is set, sets `Handled` first, lets nothing escape, and leaves alone what a handler subscribed before it handled | A click's exception ends the process, or a failing filter, logger or dialog does in its place; an app's own handler subscribed first has its decision overruled; with the switch off, an exception that should end the process is swallowed | `UiThreadExceptionHandlerTests.A_non_benign_exception_is_logged_at_Error_shown_and_handled`, `Two_calls_install_one_handler`, `With_HandleUiThreadExceptions_off_an_exception_escapes_unlogged_and_is_still_remembered`, `With_both_switches_off_nothing_is_installed`, `A_throwing_filter_does_not_escape_and_the_exception_counts_as_not_benign`, `A_throwing_logger_does_not_escape_and_the_dialog_is_still_shown`, `An_exception_another_handler_already_handled_is_left_alone_and_its_second_report_known` |
| A benign UI-thread exception is logged at Debug and shows nothing; any other is logged at Error and shown, one dialog at a time; a repeat is counted and never logged in full again, and no failure is shown twice, one first seen while another dialog was open being shown at its first repeat after that dialog closes | The Linux D-Bus portal noise alarms the user, an exception on every frame opens windows without end and rolls the log past its last file, or a failure that first came while a dialog was open is never shown | `UiThreadExceptionHandlerTests.Every_row_of_the_shared_table_is_logged_and_shown_as_its_verdict_says`, `A_second_exception_while_the_dialog_is_open_is_logged_not_shown_and_one_after_it_closes_is_shown`, `A_dialog_that_fails_to_show_reopens_the_gate`, `A_repeat_is_logged_once_counted_never_shown_twice_and_its_count_written_when_its_dialog_closes`, `A_failure_first_seen_while_a_dialog_is_open_is_shown_at_its_first_repeat_after_it_closes_and_never_again`, `A_failure_whose_dialog_failed_to_show_is_not_offered_it_again`, `FlushLog_writes_the_count_of_every_repeat_since_its_last_count_line` |
| `EntryPointOptions()` passes the host's filter on, read when an exception arrives, and on that path alone calls benign an exception that is nothing but cancellations; Avalonia's second report of an exception the UI thread let escape, whoever handled it, is neither logged, shown nor written on stderr while either switch is on | The filter set in `ConfigureLogging` never reaches stderr's decision, or an app nulls the callback believing it silences stderr; every cancellation nobody awaited opens a dialog; or every UI-thread failure, each repeat included, is reported again at a later collection, on stderr and in a second dialog | `AvaloniaDiagnosticsEntryPointTests.The_filter_reads_false_before_ConfigureLogging_and_the_host_s_answer_after_it`, `The_filter_calls_a_cancellation_benign_without_asking_the_host_s_filter`; `UiThreadExceptionHandlerTests.Avalonia_s_second_report_of_a_handled_exception_is_neither_logged_shown_nor_written_on_stderr`, `Avalonia_s_second_report_is_neither_logged_shown_nor_written_on_stderr_whoever_handled_it` |
| `EntryPointOptions()`' callback logs a benign task exception nobody observed at Debug and shows nothing; while `ShowUnobservedTaskExceptions` is set, it logs any other at Error and shows it through the UI-thread handler's gate and repeat table, the failure being the task's one exception, the dialog posted to the dispatcher `InstallAvaloniaHooks()` recorded and never waited for; with it false, at Warning with no dialog | A background failure nobody awaited goes unseen in a desktop app; one failure shows two dialogs, or two show at once; a finalizer waits for the user; a dialog posted to a dispatcher bound to the finalizer thread never opens | `AvaloniaDiagnosticsEntryPointTests.The_callback_logs_every_row_of_the_shared_table_at_the_level_its_verdict_gives`; `UiThreadExceptionHandlerTests.Every_row_abandoned_in_a_task_is_logged_shown_and_written_on_stderr_as_its_verdict_says`, `One_gate_for_both_paths_and_an_unobserved_failure_not_shown_then_is_shown_at_its_first_repeat`, `One_failure_from_both_paths_is_logged_in_full_once_and_shown_once`, `A_failure_reported_twice_before_its_dialog_opens_is_shown_once`, `Before_InstallAvaloniaHooks_a_task_exception_is_logged_and_not_shown` |
| With `FailOnUnobservedTaskExceptions` set, `InstallAvaloniaHooks()` subscribes to the lifetime's `Exit`, where two rounds of collection run, and an exit code of 0 becomes 1 when `EntryPointOptions()`' callback has counted, from the start of the run, a task exception nobody observed that is not benign. Avalonia's second report and a benign exception are not counted, the second report under a smoke run's switches too, since the switch subscribes the UI-thread handler that remembers it; a code the app set is kept, and with the switch off the code is left alone | A smoke run passes over a background failure that no collection found before it ended; or fails on noise, or on the second report of a failure the UI thread already reported; or loses the app's own failure code | `FailOnUnobservedTaskExceptionsTests.Every_row_abandoned_in_a_task_and_found_by_the_exit_handler_fails_the_run_as_its_verdict_says`, `Avalonia_s_second_report_of_a_UI_thread_exception_is_not_counted`, `A_task_a_finalizer_abandons_during_the_first_round_is_found_by_the_second`, `A_task_exception_reported_earlier_in_the_run_is_counted`, `A_code_the_app_set_is_kept`, `With_the_switch_off_the_exit_handler_leaves_the_code_alone`, `A_lifetime_that_raises_no_Exit_gets_a_Warning_only_while_the_switch_is_on`; `DesktopProbeTests`, which runs a desktop app on a real lifetime as a child process |
| Each project reaches only what its tier allows: only `AppServices.AvaloniaUI` sees Avalonia, and `AppServices.Logging` sees Serilog and nothing else | A lower package drags a UI framework into every consumer | `LayeringTests.Tiers`; `LayeringTests.Every_project_under_src_has_a_tier`; `LayeringTests.No_project_declares_a_package_its_tier_forbids`; `AssemblyQualityTests.BNAQ1003_no_assembly_references_what_its_tier_forbids` |
| Nothing here references another family (`LayeringTests.ForeignFamilies`) | The families can no longer be versioned or abandoned independently | `LayeringTests.Nothing_here_reaches_another_family`, `LayeringTests.No_compiled_assembly_reaches_another_family` |
| Every shipped assembly passes the family's AssemblyQuality rules `BNAQ1001`–`BNAQ1005` | A package ships a violation nothing ran | `AssemblyQualityTests`, one test per rule and `Every_shipped_assembly_is_in_the_scan` |
| No public method takes a defaulted `CancellationToken`, or is a token-less overload of one that takes it | A caller silently opts out of cancellation | `AssemblyQualityTests.BNAQ1001_no_public_method_takes_a_defaulted_cancellation_token` |
| No public signature names a `Microsoft.Win32`, `System.Runtime.InteropServices` or `System.Diagnostics` type | A contract becomes Windows-shaped, or a test seam becomes API, and every consumer binds against platform plumbing it never chose | `AssemblyQualityTests.BNAQ1002_no_platform_or_leak_prone_type_appears_in_the_public_surface` |
| No namespace segment shadows a referenced root, so the Avalonia assembly and namespaces are `AppServices.AvaloniaUI` while its package id stays `Bennewitz.Ninja.AppServices.Avalonia` | Under a `….Avalonia` namespace, `Avalonia.X` resolves against it instead of Avalonia's root; a changed package id strands every consumer on the old one | `AssemblyQualityTests.BNAQ1004_no_namespace_segment_shadows_a_referenced_root`; `PackageId` in `src/AppServices.AvaloniaUI/AppServices.AvaloniaUI.csproj` |
| Every interactive control in every window the Avalonia package builds has a name its automation peer reports, a tooltip that is not blank and an automation id unique in its window, by the walker's own definition of an interactive control, and no name the package gives holds a symbol | A screen reader reads a glyph's name or nothing, nothing tells the person pointing at a control what it does, and a harness cannot find it; XamlQuality's `BNXQ1011` cannot see a control built in C# | `DialogAccessibilityTests.Every_interactive_control_is_named_by_its_peer`, `Every_interactive_control_has_a_tooltip`, `Every_interactive_control_has_an_automation_id_unique_in_its_window`, each over every window in the tests' `DialogWindows`, and `Every_window_class_the_package_declares_is_in_the_table` |
| Every colour the Avalonia package gives a window it builds is one of its ten `AppServices.*` keys, declared for Light, for Dark and, with the Light values, for any other theme variant, each reading against its stand-in window at 4.5:1 for text and 3:1 for a glyph, and a destructive label at 4.5:1 on each of its fills; the first window built in an application that reads a key merges the package's dictionary there, once, first among its merged dictionaries, so a key the host declares is found first; an open window follows a change of theme, a destructive button's states included | A dialog is unreadable on a dark window, or ignores the host's colours; a destructive button's label all but vanishes when the theme repaints the button; a dictionary shared between applications throws in the second | `DialogColorTests.The_dictionary_declares_every_key_for_Light_Dark_and_Default_and_nothing_else`, `Each_colour_reads_against_its_stand_in_window_at_its_threshold`, `The_destructive_label_reads_as_text_on_each_of_its_fills_in_both_themes`, `Each_window_takes_each_colour_from_the_package_s_dictionary_and_follows_a_change_of_theme_while_open`, `Under_a_theme_that_inherits_neither_Light_nor_Dark_each_window_takes_the_Light_colours`, `Every_colour_follows_a_change_of_theme_while_open_a_destructive_button_s_in_each_of_its_states`, `Under_Semi_a_destructive_button_is_filled_from_its_keys_in_each_state_and_its_label_stays_white`, `A_key_the_host_declares_wins_over_the_package_s`, `Building_each_window_that_reads_a_key_twice_merges_one_dictionary_first_among_the_merged_ones`, `A_second_application_gets_a_dictionary_of_its_own`, each over every window in the tests' `DialogWindows` |
| Every string a window the Avalonia package builds shows that no caller passes is read through `DialogStrings.Resolver` when the window is built, and so are the pickers' titles, the fatal dialogs' titles and message, and a confirmation's labels, each where no caller passes one; a resolver that throws, or gives null, blank or text whose `{0}` cannot be filled, gives that string its default and one Warning for its key; no default names a product, and no key is renamed or removed once released | A translated app shows English in its dialogs; a host's broken strings cost the user the fatal dialog, which shows after something has already failed; another product's name ships in every host; a host's resolver stops matching a key it was written for | `DialogStringsTests.Every_string_the_package_gives_in_a_window_is_the_host_s`, over every window in the tests' `DialogWindows`, `Every_key_is_shown_in_a_window_of_the_table_or_held_by_a_test_of_its_own`, `A_resolver_that_fails_gives_each_string_its_default_and_one_Warning_for_each_key`, `The_fatal_dialog_is_built_with_its_defaults_when_the_resolver_and_the_logger_both_throw`, `No_default_names_a_product`, `Every_key_has_a_property_and_the_plan_s_default_and_there_is_no_other_key`; a pack with `EnablePackageValidation` against the last release, run by hand |
| Every packable project states `IsPackable`, a `Description` and `PackageTags`, with no `TODO`, and the packed README, `docs/appservices.md`, has no placeholder | Placeholder metadata is published and can never be replaced | `PackageMetadataTests` |
| Avalonia is pinned at 12.1.3 or later | UI Automation selection stops reaching the client on Windows; the headless tests cannot see it | `Directory.Packages.props`, `UI` group comment; `Avalonia.Headless` pinned to the same version |

## AppServices's commands

```bash
dotnet publish tests/EntryPointProbe/EntryPointProbe.csproj -c Release -r <rid> -p:PublishAot=true -warnaserror
```

The native AOT publish links with the platform's C toolchain: clang and zlib on Linux, the Visual
Studio C++ tools on Windows. Visual Studio 18 Build Tools' `vcvarsall.bat` calls `vswhere.exe` by
name, so put `%ProgramFiles(x86)%\Microsoft Visual Studio\Installer` on PATH first, or the link fails
with its "is not recognized" message where the linker's path should be.

## Adding a AppServices package

What the shared checklist above leaves out:
1. Add the project under `src/`, with `IsPackable`, a `PackageId` of `Bennewitz.Ninja.AppServices.<Name>`,
   an `AssemblyName` equal to the project file name, a `Description`, `PackageTags` and
   `GenerateDocumentationFile`.
2. Run `dotnet run --file scripts/repo-conventions.cs -- grants`, so the generated grants name the new
   assembly; `check` fails until they do.
3. Add its row to `LayeringTests.Tiers`: what it may reference and which packages it must not.
   `LayeringTests.Every_project_under_src_has_a_tier` fails until it has one.
4. Add its id to `packages.push`, or to `packages.local` with the reason as a comment above it. The
   trusted-publishing pattern `Bennewitz.Ninja.AppServices*` already covers a `Bennewitz.Ninja.AppServices.` id;
   an id outside it needs the policy widened first, see `docs/publishing.md`.
5. Add it to the package table in `docs/appservices.md` and to AppServices' packages in the root
   `README.md`, and record it in `PROGRESS.md`.

# ScopedEditors

## What ScopedEditors is

Schema-driven property editors for structured data whose values can be set at more than one scope,
where one scope overrides another. The code moved here from OpenForge2k's `LayeredEditors`
projects; every rename since is recorded in Bennewitz.Ninja.Templates'
`docs/layered-editors-renames.md`. It ships these packages to nuget.org, each listed in
`packages.push`:

| Package id | Assembly | What it ships |
|---|---|---|
| `Bennewitz.Ninja.ScopedEditors.Abstractions` | `ScopedEditors.Abstractions` | The scope and override model and the severity model (`IEditorSchema`, `IEditorScope`, `IEditorValue`, `IEditorWorkspace`, `AppSeverity`, `DangerAssessment`). References nothing |
| `Bennewitz.Ninja.ScopedEditors.ViewModels` | `ScopedEditors.ViewModels` | Property-editor view-models, factories, navigation nodes and messages, with no UI-framework reference |
| `Bennewitz.Ninja.ScopedEditors.Avalonia` | `ScopedEditors.AvaloniaUI` | Avalonia views, converters and behaviours, the bundled JetBrains Mono fonts, and the opt-in `Themes/SemiBundle.axaml` |

The consumers are Avalonia desktop applications in OpenForge2k, which take
`Themes/SemiBundle.axaml` from their `App.axaml`; `HeadlessTestApp` loads the same include. A test
or a non-UI host references `.Abstractions` or `.ViewModels` alone.

## ScopedEditors's invariants

| Invariant | Failure if broken | Guarded by |
|---|---|---|
| The Avalonia package's id is `Bennewitz.Ninja.ScopedEditors.Avalonia`; its assembly and namespaces are `ScopedEditors.AvaloniaUI` | A namespace segment named `Avalonia` shadows Avalonia's own root namespace (CS0234); renaming the id instead strands every consumer on a dead id | `PackageId` and `AssemblyName` in `src/ScopedEditors.AvaloniaUI/ScopedEditors.AvaloniaUI.csproj`; `AssemblyQualityTests.BNAQ1004_no_namespace_segment_shadows_a_referenced_root`; `PackagingTests.Every_packable_project_is_classified` |
| Every `avares://` URI names the assembly, `avares://ScopedEditors.AvaloniaUI/…`, never the package id | A `StyleInclude` fails a host's build with `AVLN2000`; a single-family `FontFamily` throws `InvalidOperationException` at first layout; a fallback list draws the wrong face silently | `BundledFontTests.Each_bundled_face_shapes_text_by_its_documented_uri`; `HeadlessTestApp` loads `Themes/SemiBundle.axaml` by its URI |
| Every input control in the markup declares a tooltip (XamlQuality `BNXQ1011`): a value input the property's `Description`, or its `DisplayName` when it has none, shown on a locked row too; fixed chrome a `WrapperStrings` `Tip*` key | Nothing tells the person pointing at an input what it does, since neither its automation name nor its id is shown; a tip bound to `Description` alone renders nothing for a property without one; a locked row disables its input, which hides a tip without `ToolTip.ShowOnDisabled` | `AxamlTooltipCoverageTests`: 12 inspected, nothing skipped, nothing found; `PropertyEditorWrapperTooltipTests`, which reads each value input's tip off rendered rows |
| `ScopedEditors.Abstractions` references nothing; `ScopedEditors.ViewModels` references no UI framework; only `ScopedEditors.AvaloniaUI` sees Avalonia or Semi | An editor can no longer be built off the UI thread, and a non-UI host inherits Avalonia | `LayeringTests` (`Tiers`, `ViewModels_compiles_without_any_UI_framework`, `Abstractions_compiles_against_nothing_but_the_framework`); `AssemblyQualityTests.BNAQ1003_no_assembly_references_what_its_tier_forbids` |
| Nothing here references the AppServices family or another repository's projects | The two families can no longer be versioned or abandoned independently | `LayeringTests.ForeignFamilies`, `Nothing_here_reaches_another_family`, `No_compiled_assembly_reaches_another_family` |
| Every shipped assembly passes the family's AssemblyQuality rules | A release ships a violation of a rule nothing ran | `AssemblyQualityTests`, including `Every_shipped_assembly_is_in_the_scan` |
| There is no bare `Bennewitz.Ninja.ScopedEditors` package | The trusted-publishing pattern `Bennewitz.Ninja.ScopedEditors.*` cannot authorise it, and it was never meant to exist | `packages.push`, header comment; `PackagingTests` |
| A rooted trimmed publish produces exactly the ILLink warnings in `trimcheck/expected-warnings.txt` | A trim hazard in compiled XAML ships unseen, or ILLink silently stops analysing `ScopedEditors.AvaloniaUI` | CI's `trim` job; `scripts/check-trim-warnings.cs` |
| Avalonia and `Avalonia.Headless` are pinned to the same version, which is the consumer's floor | A lower Avalonia loses the UI Automation selection fix a host relies on; a mismatched headless platform trips central transitive pinning | `Directory.Packages.props`, `UI` and `Testing` groups |
| The family's README, `docs/scopededitors.md`, is the nuget.org description and carries no placeholder | The placeholder is published permanently on every id | `Directory.Build.props` packs each project's `FamilyReadme`, `docs/scopededitors.md`, as `README.md`; `PackageMetadataTests.The_packed_readme_is_not_the_template_placeholder` reads that file |

## ScopedEditors's commands

```bash
dotnet publish trimcheck/TrimCheck.csproj -c Release -r linux-x64 | tee trim-publish.log
dotnet run scripts/check-trim-warnings.cs -- trim-publish.log trimcheck/expected-warnings.txt
```

`TrimCheck.csproj` is not in `AppKit.slnx`; only the publish above builds it.

## Adding a ScopedEditors package

What the shared checklist above leaves out:
1. Add the project under `src/`, with `IsPackable`, `PackageId`, `AssemblyName`, `Description` and
   `PackageTags` set in its csproj (`PackageMetadataTests`).
2. Add it to `LayeringTests.Tiers` and `TrimmerRootAssembly` in `trimcheck/TrimCheck.csproj`.
3. Add its id to `packages.push`, or to `packages.local` with the reason as a comment above it.
4. Confirm the trusted-publishing policy's `Bennewitz.Ninja.ScopedEditors.*` pattern covers a new
   `packages.push` id; see `docs/publishing.md`.
5. Add it to the package table in `docs/scopededitors.md` and to ScopedEditors' packages in the root
   `README.md`, and record it in `PROGRESS.md`.
