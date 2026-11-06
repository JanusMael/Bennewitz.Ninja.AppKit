# AGENTS.md — `scripts/`

File-based C# apps. `repo-conventions.cs`, `release-target.cs` and `assert-packages.cs` are copies of
their files in `templates/bbpkg/scripts/` in Bennewitz.Ninja.Templates, never edited here. The rest
are each family's own tools.

## AppServices

File-based C# apps, run with `dotnet run`. Each is compiled with this repository's
`Directory.Build.props`, so warnings are errors here too.

| Script | What it does | Run by |
|---|---|---|
| `assert-packages.cs` | Checks that the packages `dotnet pack` actually produced are exactly the ones `packages.push` and `packages.local` declare | CI's `pack` job, and the release step `Assert packed matches declared` before it publishes |
| `repo-conventions.cs` | Checks, and applies, the family's repository conventions: documentation, GitHub settings and rulesets, and with `grants` writes the solution-wide friend grants | CI's `conventions` job, and the maintainer |

### Rules

| Rule | Why |
|---|---|
| **`repo-conventions.cs` is never edited here** | It is a copy of `templates/bbpkg/scripts/repo-conventions.cs` in Bennewitz.Ninja.Templates, identical in every family repository. Change it there and copy it back; run from that repository, `check --repo` reports every copy that differs |
| `assert-packages.cs` reads each id from the `.nuspec` inside the package, never from the file name | `<id>.<version>.nupkg` cannot be split reliably: nothing separates an id ending in `.Widget` from one ending in `.Widget.2026` |
| `assert-packages.cs` fails on an empty or missing packages directory | A release that publishes nothing is a failure, not a no-op |
| A file-based app is compiled trimmed | Reflection-based serialisation fails the build with `IL2026`; `repo-conventions.cs` builds JSON with `System.Text.Json.Nodes` for that reason |
| Run `repo-conventions.cs` with `dotnet run --file` | Beside a `.csproj`, `dotnet run <file>` binds to the project instead |
| A file-based app never links the grant files | Linked, a missing or broken grant file would stop `repo-conventions.cs` itself compiling, so `grants` could never repair it | the `FileBasedProgram` condition in the root `Directory.Build.targets` |

## ScopedEditors

File-based C# apps, run with `dotnet run`. Each is compiled with this repository's
`Directory.Build.props`, so warnings are errors here too.

| Script | What it does | Run by |
|---|---|---|
| `assert-packages.cs` | Checks that the packages `dotnet pack` actually produced are exactly the ones `packages.push` and `packages.local` declare | CI's `pack` job, and the release before it publishes |
| `check-trim-warnings.cs` | Compares the ILLink warnings in a trimmed-publish log with `trimcheck/expected-warnings.txt`, failing on a warning added or gone | CI's `trim` job |
| `repo-conventions.cs` | Checks, and applies, the family's repository conventions: documentation, GitHub settings and rulesets | CI's `conventions` job, and the maintainer |

### Rules

| Rule | Why |
|---|---|
| **`repo-conventions.cs` is never edited here** | It is a copy of `templates/bbpkg/scripts/repo-conventions.cs` in Bennewitz.Ninja.Templates, identical in every family repository. Change it there and copy it back; run from that repository, `check --repo` reports every copy that differs |
| `assert-packages.cs` reads each id from the `.nuspec` inside the package, never from the file name | `<id>.<version>.nupkg` cannot be split reliably: nothing separates an id ending in `.Widget` from one ending in `.Widget.2026` |
| `check-trim-warnings.cs` compares warnings as a set, with the location and the bracketed project path removed | The same warning must compare equal on every machine and runtime identifier, and one text can print twice |
| An empty publish log fails `check-trim-warnings.cs` while the baseline has entries | Every expected line is reported as gone, so a publish that analysed nothing cannot read as clean |
| `repo-conventions.cs` runs with `dotnet run --file` | In a directory holding a `.csproj`, `dotnet run <file>` binds to the project instead; the family's command is written so it works in every repository |
| A file-based app is compiled trimmed | Reflection-based serialisation fails the build with `IL2026`. Build JSON with `System.Text.Json.Nodes`, and a `JsonArray` through its constructor |

## DiffView

The tools. Each is a .NET file-based app, `<name>.cs`, with a `<name>.sh` and a `<name>.ps1` beside
it that run it from either shell; `repo-conventions.cs` is run with `dotnet run --file`, as its
template prescribes. They compile with the root `Directory.Build.props`, so warnings are errors here
too, and the root `Directory.Build.targets` links no friend grants into them (`FileBasedProgram`).

| Tool | What it does | Run by |
|---|---|---|
| `catch-crash` | Judges a test run from its summary's outcome rather than its counts, or re-runs the suite until it catches an abort; `--expect auto` makes a short total a failure | anyone, after a test run |
| `drive-demo` | Drives the running demo: launch, key, click, drag, capture, hover, probe. `drive-demo-uia.ps1` is its UI Automation reader and `drive-demo-interactive.ps1` the attended pass, both Windows-only | by hand; root `AGENTS.md` §9 |
| `fetch-reference` | Clones the sources `reference/sources.json` pins | CI, before every restore |
| `pack-diagnostics` | Packs ClaudeForge's diagnostics package into the local feed the demo restores from | CI, before every restore |
| `gen-strings` | Writes the neutral `Strings.resx` from `DiffViewStrings.EnglishDefaults` | after a string changes |
| `gen-locale-review` | Writes `docs/locale-review/<culture>.md`, and `--check` compares | after a translation changes |
| `gen-template-parts` | Rewrites `docs/template-parts.md`: runs the test that generates it and takes the file the test wrote, and moves nothing when it wrote none. `.sh` and `.ps1` only | after a template edit, or a bump that moves the reference |
| `measure-menu-width` | Each locale's widest menu entry against English's, in display columns | by hand |
| `mutate-gates` | Proves the static-analysis gates able to fail; `--guards` is its standing half | CI (`--guards`), and a full run after any change to a gate file, a gated subject or a pin |
| `mutate-behaviour` | Proves behaviour tests able to fail, from the sets under `tests/*/Mutations/` | after a change a set owes, `--owed <ref>` |
| `repo-conventions` | Checks, and applies, the family's repository conventions | CI's `conventions` job, the release, and the maintainer |
| `run-demo` | Runs the demo with its flags; `.sh` and `.ps1` only | by hand |

### Rules

| Rule | Why | Guarded by |
|---|---|---|
| **`repo-conventions.cs` is never edited here** | It is a copy of `templates/bbpkg/scripts/repo-conventions.cs` in Bennewitz.Ninja.Templates, identical in every family repository: a change is made there and copied back | `check --repo`, run from Templates |
| A `.sh` is committed executable, `100755` | `core.fileMode` is false on the Windows box, so a new one commits `100644` unless it is marked with `git update-index --chmod=+x`; `git ls-files -s scripts` shows the mode | review |
| Nothing in the worktree is edited while `mutate-gates` runs, or `mutate-behaviour` outside `--parallel` | Each reverts what it mutates, and an edit outside its scope reads as a mutation's stray | root `AGENTS.md` §5 |
| ⛔ The harness's own set never runs from `mutate-behaviour.cs` itself | Its tests `dotnet run` that file, which rebuilds the binary a harness started from it runs from — locked on Windows | root `AGENTS.md` §5 |
| `fetch-reference` and `pack-diagnostics` run before any restore that includes the demo | The demo's `LayeredEditors.Avalonia.Diagnostics` comes from the local feed `NuGet.config` names, which `pack-diagnostics` fills | every job in `.github/workflows/ci.yml` |
