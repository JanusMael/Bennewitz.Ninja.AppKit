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
  `LE.*` check (`3ee1593`, `14bd2a9`). Issue #5, filed 2026-09-25 from OpenForge2k's drift 12,
  asked for three of these guards again; it was closed that day with a planted-defect re-proof on
  `c1a26bd`, and ClaudeForge's session was told so it can close the drift.
- Verified: the nuget.org flat-container lists `2026.3.924` for all three ids (checked 2026-09-24,
  again 2026-09-25).
- Consumed: OpenForge2k pins `2026.3.924` from nuget.org since its `plans/00005` merged on
  2026-09-24 as ClaudeForge #77, the consumer stage two that waited for this release.

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

- **The README names the package where it means the package.** Its Semi warning said
  `.AvaloniaUI`, the assembly; the dependency belongs to the package
  `Bennewitz.Ninja.ScopedEditors.Avalonia`. The README is packed, so this reaches nuget.org with
  the next release.

- **The family's standard build properties** (`plans/00004` in Bennewitz.Ninja.Templates):
  `IsContinuousIntegration` is gone and AutoVersioning is `2026.3.916` (`3f44048`);
  `scripts/repo-conventions.cs` evaluates every project against the family's build properties
  (`c0c331c`, `a01aa2a`); and `.github/repository.json` requires trimming, so the check fails if a
  library loses `IsTrimmable` or turns `EnableTrimAnalyzer` off (`3a20d6a`). Build and CI only.

- **The `nuget` topic is required only where `packages.push` names an id** (Templates `fb6961a`):
  `scripts/repo-conventions.cs` is the template's current copy. CI only.

- **AssemblyQuality `2026.3.925`, whose rule ids are `BNAQ1001` to `BNAQ1004`**, formerly `AQ*`:
  the family's rule ids are BN plus the product's initials. Test-only, so no package changes. The
  four tests and every mention are renamed. `BNAQ1002` now counts only what could fire, and no
  shipped assembly references a covered JSON namespace, so its zero is accepted with the reason
  beside it, as `BNAQ1001`'s already was; a planted public method returning `JsonNode` still fails
  it. Every rule also asserts `Skipped` is empty, proven by hiding `Semi.Avalonia.dll` from the
  test output. Hiding `CommunityToolkit.Mvvm.dll` instead made three rules throw rather than skip,
  which is AssemblyQuality's to fix and was reported to its session; a throw still fails here.

## Next

1. **XamlQuality's rule ids take the family prefix in its next release**: `XQ1001` to `XQ1005`
   become `BNXQ1001` to `BNXQ1005`, numbers unchanged (Bennewitz.Ninja.XamlQuality #29, merged
   2026-09-25). Nothing here keys on an id. The pinned `2026.3.922` and the newest published,
   `2026.3.924`, still report `XQ`. With the first pin past `2026.3.924`, update
   `AxamlAccessibilityCoverageTests` (comments and one assertion message),
   `ExpanderAutomationNameTests`, `src/AGENTS.md` and `tests/AGENTS.md`, as the AssemblyQuality
   bump did for its ids. The mentions in this file are history and stay as written.
2. **Per-assembly headless isolation is unverified.** `[assembly: AvaloniaTestIsolation(
   AvaloniaTestIsolationLevel.PerAssembly)]` is the candidate fix for the cross-thread failure seen
   on OpenForge2k's CI, and waits for evidence before it changes what every test shares. OpenForge2k
   has not reproduced the failure since its xUnit move (measured 2026-09-25) and parked its own
   `PerAssembly` attempt on a local branch because it broke a seam, so the evidence has not arrived.
