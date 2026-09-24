# AGENTS.md — `src/`

The shipped projects. Everything here becomes a package, so everything here is permanent once
released.

| Project | Package id | What it ships | May reference |
|---|---|---|---|
| `ScopedEditors.Abstractions` | `Bennewitz.Ninja.ScopedEditors.Abstractions` | Editor schemas, scopes, values, workspaces, `IDangerClassifier` and the severity model | nothing |
| `ScopedEditors.ViewModels` | `Bennewitz.Ninja.ScopedEditors.ViewModels` | `PropertyEditorViewModel` and its typed subclasses, `DefaultPropertyEditorFactory`, `CompositePropertyEditorFactory`, `NavigationNodeViewModel`, `Messages/` | `.Abstractions`, `CommunityToolkit.Mvvm` |
| `ScopedEditors.AvaloniaUI` | `Bennewitz.Ninja.ScopedEditors.Avalonia` | `Controls/PropertyEditorWrapper.axaml`, `Converters/`, `Behaviors/`, `Themes/` (`SemiBundle.axaml`, `EditorColors.axaml`, `AccessibilityNames.axaml`), `Localization/WrapperStrings.cs`, `Assets/Fonts/` | both of the above, Avalonia, DataGrid, Semi |

## Rules

| Rule | Why | Guarded by |
|---|---|---|
| The Avalonia project's `PackageId` keeps `.Avalonia`; its `AssemblyName`, folder and namespaces stay `.AvaloniaUI` | A namespace segment `Avalonia` shadows Avalonia's root namespace; the id is what consumers already reference | `AssemblyQualityTests.AQ1004_no_namespace_segment_shadows_a_referenced_root`; `PackageMetadataTests.Every_assembly_name_matches_its_project_file` |
| Namespaces are `Bennewitz.Ninja.<AssemblyName>`; assembly names are unprefixed | The family's naming convention | the root `Directory.Build.props`, `RootNamespace` |
| `ScopedEditors.ViewModels` takes no Avalonia or Semi reference, not even for a `Dispatcher` | A control reference being a build error is what makes an editor safe to build off the UI thread | `LayeringTests.ViewModels_compiles_without_any_UI_framework`, `No_project_declares_a_package_its_tier_forbids` |
| Semi.Avalonia is referenced only by `ScopedEditors.AvaloniaUI`, with `PrivateAssets=""` | Its package, `Bennewitz.Ninja.ScopedEditors.Avalonia`, is the one id in the family that binds a consumer to Semi, by design | `LayeringTests.Tiers`; the csproj comment |
| Every font file and `OFL.txt` stays an `AvaloniaResource`, reached as `avares://ScopedEditors.AvaloniaUI/Assets/Fonts#<family>` | A font dropped from the project resolves to a neighbouring face without a signal; OFL 1.1 requires the licence to travel with the fonts | `BundledFontTests.Each_bundled_face_shapes_text_by_its_documented_uri` |
| Every `LE.*` key referenced in markup or resolved from C# (`BrushHelper.Resolve`) is declared in `Themes/EditorColors.axaml`; every declared key is used here or listed as kept for hosts | An unresolved `DynamicResource` leaves the control unstyled with no error; `BrushHelper.Resolve` falls back to its hex silently. Deleting a declared key breaks a host that resolves it | `ThemeResourceIntegrityTests`, including `KeysKeptForHosts` and its self-check |
| A template that renders a `PropertyEditorViewModel` binds `HasDangerSeverity` and `DangerAccessibleText`, or hosts `PropertyEditorWrapper`, which renders the `IsDangerNow` banner | A Critical setting renders as an ordinary row while the view-model tests stay green | `DangerSurfaceMarkupTests` |
| A severity glyph's `FontSize` comes from `AppSeverityToFontSizeConverter` with a numeric `ConverterParameter`, never a literal | The converter falls back rather than throwing, so a typo draws a plausible wrong size | `SeverityGlyphFontSizeMarkupTests` |
| Every interactive control and every `Expander` in the markup declares `AutomationProperties.Name`; fixed chrome takes a `WrapperStrings` value | A screen reader announces a type name; `Themes/AccessibilityNames.axaml` has no name to copy onto an Expander's header | `AxamlAccessibilityCoverageTests` (XamlQuality `XQ1002`); `ExpanderAutomationNameTests` (`XQ1001`); `TemplatePartAutomationNameTests` |
| `WrapperStrings` defaults name no product | A host that never sets `WrapperStrings.Resolver` renders them verbatim | `WrapperStringsNeutralityTests` |
| `AssemblyInfo.cs` grants `InternalsVisibleTo` to `ScopedEditors.Tests`, the test assembly that exists | A wrong name fails silently: the grant goes nowhere | `FileDropTests`, which calls `FileDrop.ParseExtensions` and `FileDrop.HasAcceptedExtension` |
| All markup lives in `ScopedEditors.AvaloniaUI` | The markup guards read that project alone (`PackageSources.Project`) | `AxamlAccessibilityCoverageTests.The_scan_covers_all_the_markup_not_one_hardcoded_folder` |
| `src/Directory.Build.props` imports the root props explicitly, and keeps `IsTrimmable` and `EnableTrimAnalyzer` on | MSBuild applies only the closest props file; the trimmable mark travels in the package, and the analyser runs here or nowhere | `PackageMetadataTests.Every_shipped_assembly_is_marked_trimmable` |
| `IsAotCompatible` stays off | It would switch on the AOT and single-file analysers, a claim nothing has measured | `src/Directory.Build.props`, comment |

⚠ **The trim analyser cannot see compiled XAML.** Avalonia's XAML compiler weaves IL after Roslyn;
only the publish in `trimcheck/` reads it.
