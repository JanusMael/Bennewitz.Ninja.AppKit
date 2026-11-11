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
```

- Tests run on Microsoft.Testing.Platform (`global.json`), so `dotnet test` takes `--solution` and
  rejects VSTest-only switches such as `--nologo`. `--no-build` needs the Release build first.
- A solution-wide switch reaches every family's tests. `--filter-class` finds nothing in the other
  assemblies and exits 8, so it takes `--project`.
- `repo-conventions.cs` reads GitHub through the `gh` CLI, so a local run needs a `gh` login.
- Write `-p:` rather than `/p:`: Git Bash on Windows rewrites a leading-slash argument into a path.
- `grants` rewrites `AssemblyInfo.InternalsVisibleTo.cs` from every project's assembly name. Run it
  after adding, removing or renaming a project; `check` fails until the list matches.
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
(Serilog sinks) and `Bennewitz.Ninja.AppServices.Avalonia` (Avalonia dialogs, pickers, the bridge
from Avalonia's logger into Serilog, and the handler for exceptions the UI thread lets escape,
`plans/00002`). The code moved here out of OpenForge2k, whose Avalonia
applications are the consumers: they reference the Avalonia package, which brings its siblings
through its project references. A test or a non-UI host references `.Abstractions` or
`Bennewitz.Ninja.AppServices` alone. `Bennewitz.Ninja.AppServices.EntryPoint`, referencing nothing, is
the entry point every app's `Main` calls, console, web and desktop alike (`plans/00001`).
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
| `HostAbortedException` passes through `AppMain` unreported | A design-time tool's normal stop is reported as a crash, and shows a desktop app's fatal dialog | `AppMainTests.A_HostAbortedException_passes_through_unreported`, `EntryPointWebProbeTests.A_stop_on_HostBuilt_passes_its_HostAbortedException_back_with_nothing_on_stderr` |
| A task exception nobody observed that `AppMainOptions.IsBenignException` calls benign stays off stderr and still reaches `OnUnobservedTaskException`; any other is reported, a filter's own throw beside it, and a failure that ends the run is reported whatever the filter says | Known noise floods stderr, a broken filter goes unseen, or a filter hides a crash | `AppMainTests.A_benign_unobserved_exception_leaves_stderr_empty_and_still_reaches_the_callback`, `A_non_benign_unobserved_exception_is_reported_as_before`, `A_throwing_filter_is_reported_along_with_the_exception`, `A_fatal_exception_is_reported_whatever_the_filter_says` |
| The benign rule's two copies, in `AppServices.EntryPoint` and `AppServices.AvaloniaUI`, give every row of one table its verdict | What reaches stderr and what the Avalonia package logs and shows disagree about what is noise | `BenignExceptionRuleTests`, over `BenignExceptionTable`, which every test of the filter reads |
| `AppMainOptions` is a `sealed record`, and stays binary compatible with `2026.4.1110` | A host rebuilds the options by hand to change one callback, and drops a member it forgot; a consumer compiled against the class fails to bind | `AppMainTests.With_changes_one_member_and_keeps_the_rest`; a pack with `EnablePackageValidation` against `2026.4.1110`, run by hand |
| `InstallAvaloniaHooks()` installs the UI-thread handler once, and not at all when `HandleUiThreadExceptions` is false; the handler sets `Handled` first, lets nothing escape, and leaves alone what a handler subscribed before it handled | A click's exception ends the process, or a failing filter, logger or dialog does in its place; an app's own handler subscribed first has its decision overruled | `UiThreadExceptionHandlerTests.A_non_benign_exception_is_logged_at_Error_shown_and_handled`, `Two_calls_install_one_handler`, `False_installs_nothing`, `A_throwing_filter_does_not_escape_and_the_exception_counts_as_not_benign`, `A_throwing_logger_does_not_escape_and_the_dialog_is_still_shown`, `An_exception_another_handler_already_handled_is_left_alone` |
| A benign UI-thread exception is logged at Debug and shows nothing; any other is logged at Error and shown, one dialog at a time; a repeat is counted and never logged in full again, and no failure is shown twice, one first seen while another dialog was open being shown at its first repeat after that dialog closes | The Linux D-Bus portal noise alarms the user, an exception on every frame opens windows without end and rolls the log past its last file, or a failure that first came while a dialog was open is never shown | `UiThreadExceptionHandlerTests.Every_row_of_the_shared_table_is_logged_and_shown_as_its_verdict_says`, `A_second_exception_while_the_dialog_is_open_is_logged_not_shown_and_one_after_it_closes_is_shown`, `A_dialog_that_fails_to_show_reopens_the_gate`, `A_repeat_is_logged_once_counted_never_shown_twice_and_its_count_written_when_its_dialog_closes`, `A_failure_first_seen_while_a_dialog_is_open_is_shown_at_its_first_repeat_after_it_closes_and_never_again`, `A_failure_whose_dialog_failed_to_show_is_not_offered_it_again`, `FlushLog_writes_the_count_of_every_repeat_since_its_last_count_line` |
| `EntryPointOptions()` passes the host's filter on, read when an exception arrives, and its callback logs a benign unobserved exception at Debug and any other at Warning | The filter set in `ConfigureLogging` never reaches stderr's decision, or an app nulls the callback believing it silences stderr | `AvaloniaDiagnosticsEntryPointTests.The_filter_reads_false_before_ConfigureLogging_and_the_host_s_answer_after_it`, `The_callback_logs_every_row_of_the_shared_table_at_the_level_its_verdict_gives` |
| Each project reaches only what its tier allows: only `AppServices.AvaloniaUI` sees Avalonia, and `AppServices.Logging` sees Serilog and nothing else | A lower package drags a UI framework into every consumer | `LayeringTests.Tiers`; `LayeringTests.Every_project_under_src_has_a_tier`; `LayeringTests.No_project_declares_a_package_its_tier_forbids`; `AssemblyQualityTests.BNAQ1003_no_assembly_references_what_its_tier_forbids` |
| Nothing here references another family (`LayeringTests.ForeignFamilies`) | The families can no longer be versioned or abandoned independently | `LayeringTests.Nothing_here_reaches_another_family`, `LayeringTests.No_compiled_assembly_reaches_another_family` |
| Every shipped assembly passes the family's AssemblyQuality rules `BNAQ1001`–`BNAQ1005` | A package ships a violation nothing ran | `AssemblyQualityTests`, one test per rule and `Every_shipped_assembly_is_in_the_scan` |
| No public method takes a defaulted `CancellationToken`, or is a token-less overload of one that takes it | A caller silently opts out of cancellation | `AssemblyQualityTests.BNAQ1001_no_public_method_takes_a_defaulted_cancellation_token` |
| No public signature names a `Microsoft.Win32`, `System.Runtime.InteropServices` or `System.Diagnostics` type | A contract becomes Windows-shaped, or a test seam becomes API, and every consumer binds against platform plumbing it never chose | `AssemblyQualityTests.BNAQ1002_no_platform_or_leak_prone_type_appears_in_the_public_surface` |
| No namespace segment shadows a referenced root, so the Avalonia assembly and namespaces are `AppServices.AvaloniaUI` while its package id stays `Bennewitz.Ninja.AppServices.Avalonia` | Under a `….Avalonia` namespace, `Avalonia.X` resolves against it instead of Avalonia's root; a changed package id strands every consumer on the old one | `AssemblyQualityTests.BNAQ1004_no_namespace_segment_shadows_a_referenced_root`; `PackageId` in `src/AppServices.AvaloniaUI/AppServices.AvaloniaUI.csproj` |
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
