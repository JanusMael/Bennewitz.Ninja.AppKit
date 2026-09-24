# Progress

Work state for Bennewitz.Ninja.ScopedEditors, updated in the same change as the work it describes.
What has stopped changing moves out rather than piling up.

## Published

| Version | Tagged | Ids |
|---|---|---|
| `2026.3.924` | 2026-09-24, `v2026.3.924` on `4e44d8b` | `.Abstractions`, `.ViewModels`, `.Avalonia` |
| `2026.3.923` | 2026-09-23, `v2026.3.923` on `b5f3c2a` | the same three |

**`2026.3.924`** is the next version of the same three ids; nothing is deprecated.

- Inside `Bennewitz.Ninja.ScopedEditors.Avalonia`, the assembly and namespaces became
  `ScopedEditors.AvaloniaUI` (`56ff954`, id restored in `6525d87`), because AQ1004 found the
  `.Avalonia` namespace segment shadowing Avalonia's root. Every `avares://` URI a host wrote
  changes with it. The consumer-facing notes are in `README.md`, "Upgrading from 2026.3.923".
- Every shipped assembly is marked trimmable (`ef446d4`), the declaration `.923` shipped without.
- Avalonia 12.1.3 (`64769ae`) and Avalonia.Controls.DataGrid 12.1.2 (`54ec9fe`) are the floor a
  consumer inherits; below it, a host's restore fails with `NU1605`.
- In the repository, guarding what ships: the original suite ported, MSTest to xunit v3
  (`507b308`); the AssemblyQuality rules run as tests (`56042b1`); the rooted trimmed publish
  against an ILLink baseline (`5b08b46`); every bundled font laid out by its URI (`ab442f7`); the
  markup guards ported from ClaudeForge, with XamlQuality's XQ1001 and XQ1002 and a C# half for the
  `LE.*` check (`3ee1593`, `14bd2a9`).
- Verified: the nuget.org flat-container lists `2026.3.924` for all three ids (checked 2026-09-24).

**`2026.3.923`** was the first publish: the three ids moved out of OpenForge2k's `LayeredEditors`
projects and renamed (`582a68e`), with the layering guards (`b5f3c2a`). Verified from the
flat-container, and consumed from a project whose `nuget.config` names only nuget.org. It shipped
without `IsTrimmable`, with the AQ1004 namespace shadowing, and with the template's placeholder
README; `.924` supersedes it.

## On `main`, not yet released

Nothing here changes a package, so none of it needs a release.

- **The headless warm-up is gone** (`33f7d3d`, PR #3). With no `[AvaloniaTestIsolation]` the
  session isolates per test, so the warm-up's application was discarded at once and the tests that
  guarded it could not fail. `HeadlessSessionTests` keeps the one that can.
- **The family conventions are checked** (`87bfbcf`, step 6 of `plans/00003` in
  Bennewitz.Ninja.Templates): `scripts/repo-conventions.cs`, `.github/repository.json`, and the
  `conventions` CI job.
- **The AI-facing documents and this file** land, step 7 of the same plan: `AGENTS.md` at the root
  and in every top-level directory, each with its `CLAUDE.md` pointer, and
  `.github/copilot-instructions.md`. In the same change, `conventions` became a required check,
  `release.yml` gained the `check --release` preflight, and its comments that called `NUGET_USER`
  a secret were corrected.

## Next

1. **Consumer stage two is OpenForge2k's**, in its `plans/00005`: its last step was waiting for
   `.924` on nuget.org, which is now there. Nothing in this repository blocks it.
2. **`Bennewitz.Ninja.AutoVersioning` is pinned at `2026.3.914` here**; the template has moved to
   `2026.3.916`. Raise it with the next change that touches `Directory.Packages.props`.
3. **Per-assembly headless isolation is unverified.** `[assembly: AvaloniaTestIsolation(
   AvaloniaTestIsolationLevel.PerAssembly)]` is the candidate fix for the cross-thread failure seen
   on OpenForge2k's CI, and waits for evidence before it changes what every test shares.
