# Progress

Work state for Bennewitz.Ninja.AppKit, updated in the same change as the work it describes. What has
stopped changing moves out rather than piling up.

## Resume

The repository grew from Bennewitz.Ninja.AppServices under `plans/00009` step 8 in
Bennewitz.Ninja.Templates: ScopedEditors' history was imported whole, and the two families now share
one solution, one set of pins and one CI. Both families first released from here on 2026-11-10, at
`2026.4.1110`, on the maintainer's word (see Published).

DiffView does not live here. The plan brought it too, and the maintainer took it out on 2026-11-06:
development stays in Bennewitz.Ninja.DiffView, which is not archived.

**Synced to Bennewitz.Ninja.Templates `2026.4.1109`** on 2026-11-09. The scripts were already that
release's; it moves the pins: AutoVersioning `2026.3.916` to `2026.4.1031`, whose diagnostics are
`BNAV00` to `BNAV04` from `2026.3.928` on; xunit.v3 `4.0.2` to `4.0.3`; and, in the tests,
AssemblyQuality `2026.4.1029` to `2026.4.1031` and XamlQuality `2026.4.1026` to `2026.4.1109`.
Avalonia stays `12.1.3`, the floor every consumer inherits. Build with warnings as errors, 466 tests
(451 passed, 15 skipped as before), pack, `assert-packages`, and `check`, which conforms, pass on
Windows; the AOT and trim jobs run in CI. Neither family's packages changed, so the sync released
nothing.

**ScopedEditors runs `BNXQ1009` and `BNXQ1010`** from 2026-11-09, by the owner's choice, in
`tests/ScopedEditors.Tests/Accessibility/GeneratedRowRuleTests.cs`: the rows a list generates are
named by what they show and draw a background of their own. Each reads the strict enum's `ComboBox`,
the one such list, with the package's assembly loaded, and requires something inspected and nothing
skipped. Canaried: the row's `TextBlock` wrapped in a `Border` fails `BNXQ1009` alone, and the rows'
`Background` set to `{x:Null}` fails `BNXQ1010` alone. XamlQuality's other unadopted rules,
`BNXQ1003` to `BNXQ1006` and `BNXQ1008`, were measured the same day and inspect nothing here: no
control themes or templates, no key bindings, no fixed or zero-size grid slots. Tests only; no
package changes.

**Next, in order:**
1. **AppServices' and ScopedEditors' next releases** are each a family release, `appservices/v…` and
   `scopededitors/v…`, on the maintainer's word when the family next changes. The policy names this
   repository and covers all eight ids: each pushed at `2026.4.1110`.

## What the merge changed for consumers

Shipped at `2026.4.1110`, each family's first release from here. Against `2026.4.1103`, the packages'
dependencies and files are identical; only these change:

| Family | Change at `2026.4.1110` |
|---|---|
| AppServices | `RepositoryUrl` and `PackageProjectUrl` name AppKit, and the packed README is `docs/appservices.md` |
| ScopedEditors | `RepositoryUrl` and `PackageProjectUrl` name AppKit, and the packed README is `docs/scopededitors.md` |

## Published

### AppServices

All five ids ship together at every version.

| Version | Tag | Verified |
|---|---|---|
| `2026.4.1110` | `appservices/v2026.4.1110` | First from AppKit; release run `42842847404`. All five ids listed by the nuget.org flat-container with a nuspec naming AppKit at `a1f42db`, and a project with nuget.org as its only source restored them |
| `2026.4.1103` | `v2026.4.1103` | All five ids listed by the nuget.org flat-container, and a project with nuget.org as its only source restored them |
| `2026.4.1031` | `v2026.4.1031` | Listed; release run `41231829335` |
| `2026.4.1001` | `v2026.4.1001` | Listed; a console app restoring from nuget.org alone built and ran through `AppMain` |
| `2026.3.924` | `v2026.3.924` | Listed |
| `2026.3.923` | `v2026.3.923` | Listed |

### ScopedEditors

The three ids ship together at every version.

| Version | Tag | Verified |
|---|---|---|
| `2026.4.1110` | `scopededitors/v2026.4.1110` | First from AppKit; release run `42842847901`. All three ids listed by the nuget.org flat-container with a nuspec naming AppKit at `a1f42db`, and a project with nuget.org as its only source restored them |
| `2026.4.1103` | `scopededitors/v2026.4.1103` | All three ids listed by the nuget.org flat-container, and a project with nuget.org as its only source restored them |
| `2026.4.1031` | `scopededitors/v2026.4.1031` | Listed; release run `41231920267` |
| `2026.3.924` | `scopededitors/v2026.3.924` | Listed. OpenForge2k consumes it from nuget.org |
| `2026.3.923` | `scopededitors/v2026.3.923` | Listed |

## Next, by family

**AppServices**
1. **Headless isolation per assembly** is unverified and waits for evidence before
   `[assembly: AvaloniaTestIsolation(AvaloniaTestIsolationLevel.PerAssembly)]` is considered.
2. `LiveLogWindowSink` and its windows stay in OpenForge2k until they can ship together; nothing here
   is scheduled for them.

**ScopedEditors**
1. **Per-assembly headless isolation is unverified**, as above. It is the candidate fix for the
   cross-thread failure seen on OpenForge2k's CI, and OpenForge2k has not reproduced that failure since
   its xUnit move (measured 2026-09-25) and parked its own attempt because it broke a seam, so the
   evidence has not arrived.

## The merge

Built on a branch, in a fresh clone of AppServices at `0c2dfae`, importing ScopedEditors at `8128f90`.

**What the plan did not foresee:**

- **AppServices and ScopedEditors run on Windows and macOS in CI for the first time**, in the one
  `build` matrix.
- **`git filter-repo` strips commit signatures**, so ScopedEditors' commit hashes changed although only
  its tags were renamed. Its archived repository keeps the original commits.
- **nuget.org's policy cannot change its repository.** The rename needed a new policy naming AppKit,
  and AppServices' old one deleted.
