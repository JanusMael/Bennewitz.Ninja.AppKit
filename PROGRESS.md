# Progress

Work state for Bennewitz.Ninja.AppKit, updated in the same change as the work it describes. What has
stopped changing moves out rather than piling up.

## Resume

The repository grew from Bennewitz.Ninja.AppServices under `plans/00009` step 8 in
Bennewitz.Ninja.Templates: ScopedEditors' and DiffView's histories were imported whole, and the three
families now share one solution, one set of pins and one CI. No family has released from here yet, and
moving is not a reason to release one (decision 21).

**Next, in order:**
1. **DiffView's first release ever**, on the maintainer's word, once the policy names this repository
   and carries its pattern. It is the only family here with no published version.
2. **AppServices' and ScopedEditors' next releases** carry the raised floors below, and are each a
   family release, `appservices/v…` and `scopededitors/v…`, on the maintainer's word when the family
   next changes.
3. **DiffView's held work:** the local branch and open item 27, which waited for this move.
   [`docs/diffview-progress.md`](docs/diffview-progress.md) is DiffView's whole work state, moved here
   unchanged, with its Resume, its open items and its running history.

## What the merge changed for consumers

Nothing a family publishes changes until it next releases; then:

| Family | Change at its next release |
|---|---|
| AppServices | `RepositoryUrl` and `PackageProjectUrl` name AppKit, and the packed README is `docs/appservices.md`. `Microsoft.Extensions.Logging.Abstractions`, referenced by no AppServices package directly, is unchanged for it |
| ScopedEditors | `RepositoryUrl` and `PackageProjectUrl` name AppKit, the packed README is `docs/scopededitors.md`, and Semi.Avalonia's floor rises from `12.1.0` to `12.1.0.1` |
| DiffView | Nothing is published yet. Its first release carries Avalonia `12.1.3`, not `12.1.2`, and `Microsoft.Extensions.Logging.Abstractions` `10.0.12`, not `10.0.11`. Its README is `docs/diffview.md`, which was `docs/hosting-diffview.md` |

## Published

### AppServices

All five ids ship together at every version.

| Version | Tag | Verified |
|---|---|---|
| `2026.4.1103` | `v2026.4.1103` | All five ids listed by the nuget.org flat-container, and a project with nuget.org as its only source restored them |
| `2026.4.1031` | `v2026.4.1031` | Listed; release run `41231829335` |
| `2026.4.1001` | `v2026.4.1001` | Listed; a console app restoring from nuget.org alone built and ran through `AppMain` |
| `2026.3.924` | `v2026.3.924` | Listed |
| `2026.3.923` | `v2026.3.923` | Listed |

### ScopedEditors

The three ids ship together at every version.

| Version | Tag | Verified |
|---|---|---|
| `2026.4.1103` | `scopededitors/v2026.4.1103` | All three ids listed by the nuget.org flat-container, and a project with nuget.org as its only source restored them |
| `2026.4.1031` | `scopededitors/v2026.4.1031` | Listed; release run `41231920267` |
| `2026.3.924` | `scopededitors/v2026.3.924` | Listed. OpenForge2k consumes it from nuget.org |
| `2026.3.923` | `scopededitors/v2026.3.923` | Listed |

### DiffView

Nothing yet. Its first release is its first ever.

## Next, by family

**AppServices**
1. **Headless isolation per assembly** is unverified and waits for evidence before
   `[assembly: AvaloniaTestIsolation(AvaloniaTestIsolationLevel.PerAssembly)]` is considered.
2. `LiveLogWindowSink` and its windows stay in OpenForge2k until they can ship together; nothing here
   is scheduled for them.

**ScopedEditors**
1. **Whether to adopt XamlQuality's other opt-in rules is the owner's call.** None reports anything
   until constructed. `BNXQ1008` inspects nothing here. `BNXQ1009` (item containers named by what they
   show), and `BNXQ1005` and `BNXQ1006`, read compiled assemblies (`WithAssemblies`) and are unmeasured
   here.
2. **Per-assembly headless isolation is unverified**, as above. It is the candidate fix for the
   cross-thread failure seen on OpenForge2k's CI, and OpenForge2k has not reproduced that failure since
   its xUnit move (measured 2026-09-25) and parked its own attempt because it broke a seam, so the
   evidence has not arrived.

**DiffView:** its open items are in [`docs/diffview-progress.md`](docs/diffview-progress.md), *Open*.

## The merge

Built on a branch, in a fresh clone of AppServices at `0c2dfae`, importing ScopedEditors at `8128f90`
and DiffView at `0ca1d54`, which the maintainer held still for it.

**What the plan did not foresee:**

- **`xunit.v3` cannot be one pin.** Decision 17 takes the highest any family uses, and `plans/00009`
  predates the move to 4.x. AppServices and ScopedEditors are on `4.0.2`; DiffView's 465
  `[AvaloniaFact]` and `[AvaloniaTheory]` tests need `Avalonia.Headless.XUnit`, which fails on 4.0
  (`docs/xunit-v3-4.md` in Bennewitz.Ninja.Templates). So the central pin is `4.0.2`, and
  `tests/DiffView.Tests.props` overrides it to `3.2.2` for DiffView's three test projects, with
  TrxReport `1.9.1`. It comes off when Avalonia supports 4.x, or those tests stop using it.
- **Every restore now needs DiffView's local feed.** `NuGet.config` names `../nuget-local`, so every CI
  job and the release fetch ClaudeForge and pack `LayeredEditors.Avalonia.Diagnostics` first, including
  a release of AppServices, which does not use it.
- **`Microsoft.Extensions.Logging.Abstractions` rose to `10.0.12`.** `Microsoft.AspNetCore.Mvc.Testing`
  `10.0.12` takes it transitively, and central transitive pinning refuses the downgrade.
- **AppServices and ScopedEditors run on Windows and macOS in CI for the first time**, in the one
  `build` matrix, which DiffView had.
- **`git filter-repo` strips commit signatures**, so ScopedEditors' commit hashes changed although only
  its tags were renamed. `.git/filter-repo/commit-map` in the import clones maps them.
