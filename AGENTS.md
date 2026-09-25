# AGENTS.md — Bennewitz.Ninja.ScopedEditors

> For anyone changing this repository, human or agent. The invariants a change must not break, the
> commands, and the checklists for recurring work. Each top-level directory has an `AGENTS.md` of
> its own for what only its files show. Work state is [`PROGRESS.md`](PROGRESS.md). What every
> repository in this family carries, and how it is checked, is prescribed in
> [`docs/repository-conventions.md`](https://github.com/JanusMael/Bennewitz.Ninja.Templates/blob/main/docs/repository-conventions.md)
> in Bennewitz.Ninja.Templates.

## What this repository is

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

## Layout

| Directory | What it holds |
|---|---|
| `src/` | The shipped projects. Every one is packable and declared in `packages.push` |
| `tests/` | `ScopedEditors.Tests`: the ported suite, the layering, packaging and markup guards, and the AssemblyQuality and XamlQuality rules run as tests |
| `trimcheck/` | `TrimCheck.csproj`, a rooted trimmed publish of every shipped assembly, and the ILLink-warning baseline it is compared against |
| `scripts/` | File-based apps: `assert-packages.cs`, `check-trim-warnings.cs` and `repo-conventions.cs` |
| `docs/` | `publishing.md`, the release runbook |
| `.github/` | The workflows, `repository.json`, and the pointer for tools that read `.github/` |

## Invariants

| Invariant | Failure if broken | Guarded by |
|---|---|---|
| The Avalonia package's id is `Bennewitz.Ninja.ScopedEditors.Avalonia`; its assembly and namespaces are `ScopedEditors.AvaloniaUI` | A namespace segment named `Avalonia` shadows Avalonia's own root namespace (CS0234); renaming the id instead strands every consumer on a dead id | `PackageId` and `AssemblyName` in `src/ScopedEditors.AvaloniaUI/ScopedEditors.AvaloniaUI.csproj`; `AssemblyQualityTests.BNAQ1004_no_namespace_segment_shadows_a_referenced_root`; `PackagingTests.Every_packable_project_is_classified` |
| Every `avares://` URI names the assembly, `avares://ScopedEditors.AvaloniaUI/…`, never the package id | A `StyleInclude` fails a host's build with `AVLN2000`; a single-family `FontFamily` throws `InvalidOperationException` at first layout; a fallback list draws the wrong face silently | `BundledFontTests.Each_bundled_face_shapes_text_by_its_documented_uri`; `HeadlessTestApp` loads `Themes/SemiBundle.axaml` by its URI |
| `ScopedEditors.Abstractions` references nothing; `ScopedEditors.ViewModels` references no UI framework; only `ScopedEditors.AvaloniaUI` sees Avalonia or Semi | An editor can no longer be built off the UI thread, and a non-UI host inherits Avalonia | `LayeringTests` (`Tiers`, `ViewModels_compiles_without_any_UI_framework`, `Abstractions_compiles_against_nothing_but_the_framework`); `AssemblyQualityTests.BNAQ1003_no_assembly_references_what_its_tier_forbids` |
| Nothing here references the AppServices family or another repository's projects | The two families can no longer be versioned or abandoned independently | `LayeringTests.ForeignFamilies`, `Nothing_here_reaches_another_family`, `No_compiled_assembly_reaches_another_family` |
| Every shipped assembly passes the family's AssemblyQuality rules | A release ships a violation of a rule nothing ran | `AssemblyQualityTests`, including `Every_shipped_assembly_is_in_the_scan` |
| There is no bare `Bennewitz.Ninja.ScopedEditors` package | The trusted-publishing pattern `Bennewitz.Ninja.ScopedEditors.*` cannot authorise it, and it was never meant to exist | `packages.push`, header comment; `PackagingTests` |
| Every packable project's id is in exactly one of `packages.push` and `packages.local` | A package nobody chose is published, permanently | `PackagingTests`; `scripts/assert-packages.cs`; the release step `Assert packed matches declared` |
| The release pushes the ids `packages.push` names and never globs `*.nupkg` | A new packable project is published by the next tag | `.github/workflows/release.yml`, step `Push to NuGet.org`; `PackagingTests.The_release_workflow_globs_nothing_and_publishes_what_is_declared` |
| Every shipped assembly declares itself trimmable | A consumer's `TrimMode=partial` publish keeps it whole and outside its trim analysis, and says nothing | `src/Directory.Build.props`; `PackageMetadataTests.Every_shipped_assembly_is_marked_trimmable` |
| A rooted trimmed publish produces exactly the ILLink warnings in `trimcheck/expected-warnings.txt` | A trim hazard in compiled XAML ships unseen, or ILLink silently stops analysing `ScopedEditors.AvaloniaUI` | CI's `trim` job; `scripts/check-trim-warnings.cs` |
| Avalonia and `Avalonia.Headless` are pinned to the same version, which is the consumer's floor | A lower Avalonia loses the UI Automation selection fix a host relies on; a mismatched headless platform trips central transitive pinning | `Directory.Packages.props`, `UI` and `Testing` groups |
| The README is the nuget.org description and carries no placeholder | The placeholder is published permanently on every id | `Directory.Build.props` packs `README.md`; `PackageMetadataTests.The_packed_readme_is_not_the_template_placeholder` |
| Packages resolve from nuget.org only | A second source added later silently starts supplying packages | `NuGet.config`, `packageSourceMapping` |
| Versions are pinned centrally | Two projects drift to different versions of one dependency | `Directory.Packages.props` |
| Warnings are errors | A warning ships | `Directory.Build.props`; `ci.yml` builds with `-warnaserror` |
| The version is the tag, `vYYYY.Q.MMDD` | The package and the tag disagree. A published version can never be replaced | `release.yml`, step `Resolve version and tag` |
| `NUGET_USER` is a repository **variable**, not a secret | A masked value hides why a trusted-publishing login fails | `release.yml`, step `Refuse to release without NUGET_USER` |
| The repository meets the family conventions | Documentation or settings go missing unnoticed | `scripts/repo-conventions.cs`, run by CI's `conventions` job |

## Commands

```bash
dotnet build ScopedEditors.slnx -c Release -warnaserror
dotnet test --solution ScopedEditors.slnx --no-build -c Release
dotnet pack ScopedEditors.slnx -c Release --output ./packages/Release
dotnet run scripts/assert-packages.cs -- ./packages/Release
dotnet publish trimcheck/TrimCheck.csproj -c Release -r linux-x64 | tee trim-publish.log
dotnet run scripts/check-trim-warnings.cs -- trim-publish.log trimcheck/expected-warnings.txt
dotnet run --file scripts/repo-conventions.cs -- check
```

- Tests run on Microsoft.Testing.Platform (`global.json`), so `dotnet test` takes `--solution` and
  rejects VSTest-only switches such as `--nologo`.
- `TrimCheck.csproj` is not in `ScopedEditors.slnx`; only the publish above builds it.
- `repo-conventions.cs` reads GitHub through the `gh` CLI, so a local run needs a `gh` login.
- Write `-p:` rather than `/p:`: Git Bash on Windows rewrites a leading-slash argument into a path.

## Checklists

**Adding a package**
1. Add the project under `src/`, with `IsPackable`, `PackageId`, `AssemblyName`, `Description` and
   `PackageTags` set in its csproj (`PackageMetadataTests`).
2. Add it to `LayeringTests.Tiers` and `TrimmerRootAssembly` in `trimcheck/TrimCheck.csproj`.
3. Add its id to `packages.push`, or to `packages.local` with the reason as a comment above it.
4. Confirm the trusted-publishing policy's `Bennewitz.Ninja.ScopedEditors.*` pattern covers a new
   `packages.push` id; see `docs/publishing.md`.
5. Add it to the package table in `README.md`, and record it in `PROGRESS.md`.

**Adding a top-level directory:** give it an `AGENTS.md` and a `CLAUDE.md` containing `@AGENTS.md`,
or exempt it in `.github/repository.json` under `undocumented`, with the reason. CI fails until
one of the two is done.

**Changing a dependency version:** in `Directory.Packages.props` only. Moving Avalonia moves
`Avalonia.Headless` with it; moving `Avalonia.Controls.DataGrid` can move the trim baseline, so run
the trim commands above. A raised floor is a consumer-visible change: say so in `README.md`.

**Releasing:** `docs/publishing.md`. Afterwards, move the released rows in `PROGRESS.md` from
"On `main`, not yet released" to "Published", with how the release was verified from the feed.

**Every change:** update `PROGRESS.md` in the same commit. Commits are Conventional Commits.
