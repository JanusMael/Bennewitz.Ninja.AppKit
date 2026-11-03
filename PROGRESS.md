# Progress

Work state for Bennewitz.Ninja.ScopedEditors, updated in the same change as the work it describes.
What has stopped changing moves out rather than piling up.

## Published

| Version | Tagged | Ids |
|---|---|---|
| `2026.4.1031` | 2026-10-31, `v2026.4.1031` on `b8c6f69` | `.Abstractions`, `.ViewModels`, `.Avalonia` |
| `2026.3.924` | 2026-09-24, `v2026.3.924` on `4e44d8b` | the same three |
| `2026.3.923` | 2026-09-23, `v2026.3.923` on `b5f3c2a` | the same three |

**`2026.4.1031`** carries everything `main` gathered since `.924`; nothing is deprecated.

- In `.Avalonia`: the property-name pill sets its tooltip once, on its `Border`, so a property with
  no `Description` no longer gives a screen reader its path. Every interactive control a property
  row draws declares an automation id bound to the property's schema path, XamlQuality's
  `BNXQ1007`; the README documents the scheme.
- In every package: the README names the package where it meant the package, drops its
  *Releasing* section and links the licence absolutely. The family's friend grants replace
  `.AvaloniaUI`'s own, and a local build records `PublicVersion` too.
- In the repository: the family's conventions, AI-facing documents and standard build properties;
  AssemblyQuality and XamlQuality `2026.3.928`, with the `BN*` rule ids; the headless warm-up gone;
  xunit.v3 `4.0.2`.
- Verified: the nuget.org flat-container lists `2026.4.1031` for all three ids, release run
  `41231920267` succeeded, and the GitHub release carries the three packages (checked 2026-11-03).

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

- **Synced to Bennewitz.Ninja.Templates `2026.4.1103`**, on 2026-11-03. `scripts/repo-conventions.cs`
  is that release's, which also checks that markdown is checked out as LF; the root `.gitattributes`
  already does. `Microsoft.VisualStudio.Threading.Analyzers` `18.12.1` is referenced privately, with
  only `VSTHRD101` and `VSTHRD110` on: the family's decision of 2026-11-03.
- **The DataGrid's *Copy value* no longer ends the host's process when a copy fails.** `VSTHRD101`
  reported `CopyValue`'s menu handler: an async lambda on `Click` is async void, so a throw from the
  clipboard reached the dispatcher. It is now a named local handler whose whole body is one
  try/catch, reporting the failure at Warning through Avalonia's logger, which the host routes
  wherever it logs; this repository has no logger of its own. Nothing is suppressed. With the lambda
  put back, the build fails at exactly that site. `check` conforms; build, 267 of 267 tests, pack,
  `assert-packages`, and the trim check's 3 expected warnings and none new pass on Windows.
  `.Avalonia`'s code changes, so it ships in the next release, on the maintainer's word.

## Next

1. **Whether to adopt XamlQuality's other opt-in rules is the owner's call.** None reports
   anything until constructed. `BNXQ1008` inspects nothing here. `BNXQ1009` (item containers named
   by what they show), and `BNXQ1005` and `BNXQ1006` from `2026.3.925`, read compiled assemblies
   (`WithAssemblies`) and are unmeasured here.
2. **Per-assembly headless isolation is unverified.** `[assembly: AvaloniaTestIsolation(
   AvaloniaTestIsolationLevel.PerAssembly)]` is the candidate fix for the cross-thread failure seen
   on OpenForge2k's CI, and waits for evidence before it changes what every test shares. OpenForge2k
   has not reproduced the failure since its xUnit move (measured 2026-09-25) and parked its own
   `PerAssembly` attempt on a local branch because it broke a seam, so the evidence has not arrived.
