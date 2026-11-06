# AGENTS.md — `trimcheck/`

A trimmed publish of every shipped assembly, run by CI's `trim` job. It is the only check that reads
compiled XAML: Avalonia's XAML compiler weaves IL into `ScopedEditors.AvaloniaUI` after Roslyn, so
the trim analyser that runs on every build never sees it. Nothing here ships, and `TrimCheck.csproj`
is not in `ScopedEditors.slnx`.

| File | What it is |
|---|---|
| `TrimCheck.csproj` | A self-contained `PublishTrimmed`, `TrimMode=full` publish that references every project under `src/` and roots each assembly with `TrimmerRootAssembly` |
| `Program.cs` | Intentionally empty; the rooting, not the code, decides what ILLink analyses |
| `expected-warnings.txt` | The ILLink warnings the publish is expected to produce, as `scripts/check-trim-warnings.cs` normalises them. Its entries are `IL2070`s in `Avalonia.Controls.Utils.TypeHelper`, a dependency's code, not this repository's |

## Rules

| Rule | Why | Guarded by |
|---|---|---|
| Every shipped assembly is a `TrimmerRootAssembly`, named by **assembly** name (`ScopedEditors.AvaloniaUI`), not package id | Unrooted code is removed before it can warn, so a clean result would mean nothing | `TrimCheck.csproj`; a new project under `src/` is added here in the same change |
| `TrimmerSingleWarn` stays `false` | One `IL2104` per assembly cannot be compared line by line | `TrimCheck.csproj` |
| `TreatWarningsAsErrors` stays `false` here | The publish measures; the baseline comparison decides | `TrimCheck.csproj`; `scripts/check-trim-warnings.cs` |
| The warnings must match `expected-warnings.txt` exactly, in both directions | A new line is a trim hazard someone added. A missing line means ILLink stopped analysing `ScopedEditors.AvaloniaUI`, because the DataGrid warnings are reachable only through it (`DataGridCopyValueBehavior`) | CI's `trim` job, step `Compare warnings with the baseline` |
| Adding a line to `expected-warnings.txt` is a decision to ship a trim hazard, and the commit says why | The baseline is the record of what was accepted | `expected-warnings.txt`, header |
| Moving `Avalonia.Controls.DataGrid` or Avalonia in `Directory.Packages.props` means re-running the publish and comparing | The baseline's warnings come from DataGrid itself | `Directory.Packages.props`, `UI` group comment |
| The publish step keeps `shell: bash` | It is what turns on `pipefail`; without it a failed publish passes through `tee` | `.github/workflows/ci.yml`, step `Publish trimmed` |
