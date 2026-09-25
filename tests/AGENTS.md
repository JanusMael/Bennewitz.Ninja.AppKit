# AGENTS.md — `tests/`

`ScopedEditors.Tests`, the repository's only test project. `tests/Directory.Build.props` makes it an
xUnit v3 test executable that is never packed. It references all of `src/`, `Avalonia.Headless`,
`Bennewitz.Ninja.AssemblyQuality` and `Bennewitz.Ninja.XamlQuality`.

| Folder | What it covers |
|---|---|
| `Architecture/` | `LayeringTests`, `AssemblyQualityTests` (BNAQ1001–BNAQ1004 over every shipped assembly), `PackageMetadataTests`, and the markup guards `ThemeResourceIntegrityTests`, `DangerSurfaceMarkupTests`, `SeverityGlyphFontSizeMarkupTests` |
| `Accessibility/` | XamlQuality's `XQ1002` (`AxamlAccessibilityCoverageTests`) and `XQ1001` (`ExpanderAutomationNameTests`) over the package's markup |
| `Packaging/` | `PackagingTests`: the package lists and the release workflow |
| `Assets/` | `BundledFontTests`: text laid out in every bundled face by its documented `avares://` URI |
| `Themes/`, `Controls/`, `Converters/`, `Behaviors/`, `Helpers/`, `Localization/`, `ViewModels/` | The suite ported with the code, organised by what it tests |
| `Headless/` | `HeadlessTestApp`, which loads `Themes/SemiBundle.axaml`, and `HeadlessSessionTests` |
| the project root | `Parallelization.cs`, `MovedSourceSmokeTests`, `ScaffoldingSanityTests` |
| `Support/`, `Fakes/` | `PackageSources` (the one definition of what the markup guards read), `MessageAssert`, and fakes of the `.Abstractions` interfaces |

## Rules

| Rule | Why | Guarded by |
|---|---|---|
| Tests run on Microsoft.Testing.Platform | `dotnet test` takes `--solution` and rejects VSTest-only switches | `global.json`, `test.runner` |
| `tests/Directory.Build.props` imports the root props explicitly | Without it the test project silently loses the root's target framework and nullable settings | the import line itself |
| Tests run one at a time | There is one headless Avalonia dispatcher per assembly, and it must not be driven concurrently | `Parallelization.cs`; `HeadlessSessionTests.GetOrStartForAssembly_ReturnsTheSameSessionEveryTime` |
| A headless test dispatches a **synchronous** body through `HeadlessUnitTestSession.GetOrStartForAssembly` | An `async` body returns a `Task<Task>` nothing awaits, so the test cannot fail | `HeadlessTestApp`, summary |
| No warm-up dispatch and no shared application across tests without `[AvaloniaTestIsolation]` evidence | With no isolation attribute every dispatch builds a fresh application, so a warm-up proves nothing | `HeadlessSessionTests`, remarks |
| Every scan asserts it inspected something before asserting it found nothing, or says beside the assertion why zero is true; an AssemblyQuality scan also asserts `Skipped` is empty | Zero findings from a scan that saw nothing, or only part of what it was given, reads as a clean codebase | `AssemblyQualityTests.Every_shipped_assembly_is_in_the_scan` and its `Skipped` assertions; `PackageSources.Markup`; the `Inspected` assertions in each markup guard |
| The markup guards read the project through `PackageSources`, with XML comments stripped | One home for the scope; a binding named only in a comment is not a binding | `PackageSources.Project`, `PackageSources.MarkupText` |
| `LayeringTests.Tiers` is the single table of what each project may reach | `AssemblyQualityTests.BNAQ1003_no_assembly_references_what_its_tier_forbids` reads it too; a copy would rot | `LayeringTests.Every_tier_named_here_actually_exists` |
| A new `LE.*` key used only by hosts goes in `ThemeResourceIntegrityTests.KeysKeptForHosts` with its reason | The list is the only exemption from the dead-token check, and it checks itself | `Every_key_kept_for_hosts_is_still_declared_and_still_unused_here` |

⛔ **Never weaken a packaging, layering or markup guard to make it pass.** When one fails, the
package list, the project or the markup is wrong, not the test. A changed guard is proven by
planting the defect it exists for, watching exactly that test fail, and removing the defect.
