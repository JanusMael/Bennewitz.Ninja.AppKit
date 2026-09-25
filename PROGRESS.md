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

Two entries change what a package carries, the README's wording and the tooltip correction, and
reach consumers with the next release. Nothing else here changes a package.

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
  it. Every rule that loads assemblies also asserts `Skipped` is empty, proven by hiding
  `Semi.Avalonia.dll` from the test output; `BNAQ1003` reads reference names and loads nothing, so
  it asserts none. Hiding `CommunityToolkit.Mvvm.dll` instead made three rules throw rather than
  skip. AssemblyQuality fixed that on its `main` (`0500123`), shipping in `2026.3.926` at the
  earliest; until then a throw still fails here.

- **XamlQuality `2026.3.925`, whose rule ids are `BNXQ1001` to `BNXQ1004`**, formerly `XQ*`;
  `BNXQ1005` and `BNXQ1006` are new rules, not run here. Test-only. Only `BNXQ1001` and `BNXQ1002`
  run here, and their mentions are renamed. The tightening since `2026.3.922` changes nothing: no
  markup writes an empty `<AutomationProperties.Name>` element. Clean build, 264 pass, and an
  unnamed `TextBox` and an unnamed `Expander` still fail, under the new ids.

- **A tooltip covers its host's children, so the property-name pill sets its tip once.** Since
  Avalonia 11.1, `ToolTipService` walks up from the element under the pointer to the nearest
  control with `ToolTip.Tip` set, as XamlQuality's `avalonia-gotchas.md` now says. The
  `PropertyNameLabel` `TextBlock` in `PropertyEditorWrapper.axaml` repeated its `Border`'s tip on
  the belief that tooltips don't propagate child→parent; the copy is gone and the comment is
  corrected. `NavigationNodeViewModel`'s remarks on `BadgeTooltip` keep the badge's own tooltip,
  now because it says something different from the row's, and no longer advise repeating the
  row's tooltip on the icon and title. Headless, on the built markup, the pointer over the label
  opens the `Border`'s tip; the label was given a transparent background so that it hit-tests
  without real text rendering. The copy did do one thing: a control's automation help text falls
  back to its own tip, so a property with no `Description` no longer gives a screen reader its
  path. Ships in `.Avalonia` and `.ViewModels` with the next release.

## Next

1. **Per-assembly headless isolation is unverified.** `[assembly: AvaloniaTestIsolation(
   AvaloniaTestIsolationLevel.PerAssembly)]` is the candidate fix for the cross-thread failure seen
   on OpenForge2k's CI, and waits for evidence before it changes what every test shares. OpenForge2k
   has not reproduced the failure since its xUnit move (measured 2026-09-25) and parked its own
   `PerAssembly` attempt on a local branch because it broke a seam, so the evidence has not arrived.
