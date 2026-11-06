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
