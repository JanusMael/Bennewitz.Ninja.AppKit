# AGENTS.md — `src/`

The shipped projects of the two families, each folder named for its family. Each family's packages are published together from that
family's tag, so everything here is permanent once its family releases.

## AppServices

The shipped projects. Everything here becomes a package, so everything here is permanent once
released.

| Project | Package id | May reference | Ships |
|---|---|---|---|
| `AppServices.Abstractions` | `Bennewitz.Ninja.AppServices.Abstractions` | nothing | `IShellLauncher`, `IShareService`, `IDialogService`, `IEnvironmentProvider`, `IPermissionPathPicker`, `ISaveChangesPrompt`, `LaunchResult`, `ShareOutcome`, `DiagnosticSink`, and `DialogMessage` under `Dialogs/` |
| `AppServices` | `Bennewitz.Ninja.AppServices` | `AppServices.Abstractions` | `ShellLauncher`, `DefaultShareService`, `DefaultEnvironmentProvider`, `NativeErrorDialog` |
| `AppServices.Logging` | `Bennewitz.Ninja.AppServices.Logging` | Serilog only | `BucketedRollingFileSink` |
| `AppServices.AvaloniaUI` | `Bennewitz.Ninja.AppServices.Avalonia` | its sibling projects, Avalonia, Serilog | `AvaloniaDialogService`, `FatalErrorDialog`, `NonFatalNoticeDialog`, `AvaloniaDiagnostics` with `AvaloniaDiagnosticsOptions` and `EntryPointOptions`, `SerilogAvaloniaSink`, `BindingValidationErrorLogger` |
| `AppServices.EntryPoint` | `Bennewitz.Ninja.AppServices.EntryPoint` | nothing | `AppMain` with `AppMainOptions`, and `UsageException` |

### Rules

| Rule | Why | Guarded by |
|---|---|---|
| **`AppServices.Abstractions` has no references at all** | It is the package a test or a non-UI host consumes; a reference here quietly undoes the split | `LayeringTests.Abstractions_compiles_against_nothing_but_the_framework`; the comment in its csproj |
| **`AppServices.EntryPoint` has no references at all** | Every app's `Main` takes it, a native AOT web API's included, so whatever it references travels into every app. The app's logger is flushed through a callback the app passes | `LayeringTests.EntryPoint_compiles_against_nothing_but_the_framework`; the comment in its csproj |
| `AppMain` never catches a `HostAbortedException`, and recognises it by its type's name | A design-time tool such as `dotnet ef` throws it inside the entry point to stop it, and may throw a private type of that name | `AppMainTests.A_HostAbortedException_passes_through_unreported`; `EntryPointWebProbeTests` |
| A console or desktop run flushes on SIGTERM through a `PosixSignalRegistration` that never sets `Cancel` | On Linux the runtime ends the process on SIGTERM without raising `ProcessExit`, so without it the log is lost; a cancelled SIGTERM would leave the app running | `EntryPointProbeTests.SIGTERM_flushes_the_log_and_the_signal_still_ends_the_process` |
| A doc comment in `AppServices.Abstractions` names an implementation with `<c>`, never `cref` | The implementation is in a package it cannot reference, so the `cref` fails the build | `GenerateDocumentationFile` with warnings as errors |
| `AppServices` and `AppServices.Abstractions` take no logger; diagnostics go through a `DiagnosticSink` passed in | Neither package may reference Serilog | `LayeringTests.Tiers`; `LayeringTests.No_project_declares_a_package_its_tier_forbids` |
| `AppServices.Logging` holds sinks that need no window | A sink that calls into a window is a UI type; `LiveLogWindowSink` stayed in OpenForge2k for that reason | `LayeringTests.Tiers`; the comment in its csproj |
| `AppServices.AvaloniaUI` → `AppServices` is an allowed edge | `AvaloniaDiagnostics.ShowNativeFatalError` wraps `NativeErrorDialog` | `LayeringTests.Tiers`; the comment in its csproj |
| The Avalonia project's package id keeps `.Avalonia`; its assembly and namespaces stay `.AvaloniaUI` | A package id is permanent; a namespace segment `Avalonia` shadows Avalonia's root | `AssemblyQualityTests.BNAQ1004_no_namespace_segment_shadows_a_referenced_root` |
| `AssemblyName` equals the project file name; namespaces are `Bennewitz.Ninja.<AssemblyName>` | The layering guards match assemblies by name, so a mismatch drops a project from its own scan | `PackageMetadataTests.Every_assembly_name_matches_its_project_file`; `RootNamespace` in the root `Directory.Build.props` |
| Every `CancellationToken` parameter is required, and no public overload leaves it out | A default, or an overload that passes `CancellationToken.None`, lets a caller skip cancellation without noticing | `AssemblyQualityTests.BNAQ1001_no_public_method_takes_a_defaulted_cancellation_token` |
| No public signature names a `Microsoft.Win32`, `System.Runtime.InteropServices` or `System.Diagnostics` type | The contracts stay platform-neutral and fakeable; a registry key or safe handle in one binds every consumer to Windows plumbing, and a `Process` in one makes a test seam API | `AssemblyQualityTests.BNAQ1002_no_platform_or_leak_prone_type_appears_in_the_public_surface` |
| No project declares a grant of its own: every one compiles the generated `AssemblyInfo.InternalsVisibleTo.cs`, which grants every assembly this repository builds, and `AssemblyInfo.InternalsVisibleTo.External.cs`, the hand-written grants to the maintainer's other repositories | `internal` means solution-internal, so a seam between two projects stays internal, and a member no other assembly may reach is `private`. None of it is a security boundary: nothing here is strong-named | `scripts/repo-conventions.cs check`; `AssemblyQualityTests.BNAQ1005_every_friend_grant_names_an_allowed_assembly` |
| `src/Directory.Build.props` imports the root props explicitly | MSBuild applies only the closest `Directory.Build.props`; without the import every project here silently loses the target framework, nullable settings, package metadata and AutoVersioning | the import line; `PackageMetadataTests.Every_shipped_assembly_is_marked_trimmable` fails if the file stops applying |
| `IsTrimmable` and `EnableTrimAnalyzer` stay on; `IsAotCompatible` stays off everywhere but `AppServices.EntryPoint` | The trimmable mark travels in the package; the analyser runs here or nowhere. AOT compatibility is claimed only where something measures it, and only `AppServices.EntryPoint` is measured: CI's `aot-linux` and `aot-windows` jobs publish it with native AOT | `PackageMetadataTests.Every_shipped_assembly_is_marked_trimmable`; `src/Directory.Build.props`, comment; `AppServices.EntryPoint.csproj`, comment |

⚠ **The trim analyser cannot see compiled XAML.** Every dialog here is built in C#, so it sees all of
them today. A project that adds `.axaml` files needs an ILLink pass as well; `src/Directory.Build.props`
explains how.

## ScopedEditors

The shipped projects. Everything here becomes a package, so everything here is permanent once
released.

| Project | Package id | What it ships | May reference |
|---|---|---|---|
| `ScopedEditors.Abstractions` | `Bennewitz.Ninja.ScopedEditors.Abstractions` | Editor schemas, scopes, values, workspaces, `IDangerClassifier` and the severity model | nothing |
| `ScopedEditors.ViewModels` | `Bennewitz.Ninja.ScopedEditors.ViewModels` | `PropertyEditorViewModel` and its typed subclasses, `DefaultPropertyEditorFactory`, `CompositePropertyEditorFactory`, `NavigationNodeViewModel`, `Messages/` | `.Abstractions`, `CommunityToolkit.Mvvm` |
| `ScopedEditors.AvaloniaUI` | `Bennewitz.Ninja.ScopedEditors.Avalonia` | `Controls/PropertyEditorWrapper.axaml`, `Converters/`, `Behaviors/`, `Themes/` (`SemiBundle.axaml`, `EditorColors.axaml`, `AccessibilityNames.axaml`), `Localization/WrapperStrings.cs`, `Assets/Fonts/` | both of the above, Avalonia, DataGrid, Semi |

### Rules

| Rule | Why | Guarded by |
|---|---|---|
| The Avalonia project's `PackageId` keeps `.Avalonia`; its `AssemblyName`, folder and namespaces stay `.AvaloniaUI` | A namespace segment `Avalonia` shadows Avalonia's root namespace; the id is what consumers already reference | `AssemblyQualityTests.BNAQ1004_no_namespace_segment_shadows_a_referenced_root`; `PackageMetadataTests.Every_assembly_name_matches_its_project_file` |
| Namespaces are `Bennewitz.Ninja.<AssemblyName>`; assembly names are unprefixed | The family's naming convention | the root `Directory.Build.props`, `RootNamespace` |
| `ScopedEditors.ViewModels` takes no Avalonia or Semi reference, not even for a `Dispatcher` | A control reference being a build error is what makes an editor safe to build off the UI thread | `LayeringTests.ViewModels_compiles_without_any_UI_framework`, `No_project_declares_a_package_its_tier_forbids` |
| Semi.Avalonia is referenced only by `ScopedEditors.AvaloniaUI`, with `PrivateAssets=""` | Its package, `Bennewitz.Ninja.ScopedEditors.Avalonia`, is the one id in the family that binds a consumer to Semi, by design | `LayeringTests.Tiers`; the csproj comment |
| Every font file and `OFL.txt` stays an `AvaloniaResource`, reached as `avares://ScopedEditors.AvaloniaUI/Assets/Fonts#<family>` | A font dropped from the project resolves to a neighbouring face without a signal; OFL 1.1 requires the licence to travel with the fonts | `BundledFontTests.Each_bundled_face_shapes_text_by_its_documented_uri` |
| Every `LE.*` key referenced in markup or resolved from C# (`BrushHelper.Resolve`) is declared in `Themes/EditorColors.axaml`; every declared key is used here or listed as kept for hosts | An unresolved `DynamicResource` leaves the control unstyled with no error; `BrushHelper.Resolve` falls back to its hex silently. Deleting a declared key breaks a host that resolves it | `ThemeResourceIntegrityTests`, including `KeysKeptForHosts` and its self-check |
| A template that renders a `PropertyEditorViewModel` binds `HasDangerSeverity` and `DangerAccessibleText`, or hosts `PropertyEditorWrapper`, which renders the `IsDangerNow` banner | A Critical setting renders as an ordinary row while the view-model tests stay green | `DangerSurfaceMarkupTests` |
| A severity glyph's `FontSize` comes from `AppSeverityToFontSizeConverter` with a numeric `ConverterParameter`, never a literal | The converter falls back rather than throwing, so a typo draws a plausible wrong size | `SeverityGlyphFontSizeMarkupTests` |
| Every interactive control and every `Expander` in the markup declares `AutomationProperties.Name`; fixed chrome takes a `WrapperStrings` value | A screen reader announces a type name; `Themes/AccessibilityNames.axaml` has no name to copy onto an Expander's header | `AxamlAccessibilityCoverageTests` (XamlQuality `BNXQ1002`); `ExpanderAutomationNameTests` (`BNXQ1001`); `TemplatePartAutomationNameTests` |
| Every interactive control declares `AutomationProperties.AutomationId`, bound to the property's `Path`: the value input takes `Path`, every other control on the row `Path#<role>`, as the table in `docs/scopededitors.md` lists | A fixed id repeats on every row, an id derived from `x:Name` changes when the field is renamed, and hosts' tests search by these strings | `AxamlAutomationIdCoverageTests` (XamlQuality `BNXQ1007`); `PropertyEditorWrapperAutomationIdTests`, which reads the ids off a rendered row |
| `WrapperStrings` defaults name no product | A host that never sets `WrapperStrings.Resolver` renders them verbatim | `WrapperStringsNeutralityTests` |
| Every project's internals are visible to every assembly this repository builds, through the root `AssemblyInfo.InternalsVisibleTo.cs` that `Directory.Build.targets` links in; no project declares a grant of its own. `internal` means solution-internal, not test-only | A hand-written grant can name an assembly that does not exist and fail silently, as this one did twice before the family's grants | `repo-conventions check`, `grants`; `FileDropTests`, which calls `FileDrop.ParseExtensions` and `FileDrop.HasAcceptedExtension` |
| All markup lives in `ScopedEditors.AvaloniaUI` | The markup guards read that project alone (`PackageSources.Project`) | `AxamlAccessibilityCoverageTests.The_scan_covers_all_the_markup_not_one_hardcoded_folder` |
| `src/Directory.Build.props` imports the root props explicitly, and keeps `IsTrimmable` and `EnableTrimAnalyzer` on | MSBuild applies only the closest props file; the trimmable mark travels in the package, and the analyser runs here or nowhere | `PackageMetadataTests.Every_shipped_assembly_is_marked_trimmable` |
| `IsAotCompatible` stays off | It would switch on the AOT and single-file analysers, a claim nothing has measured | `src/Directory.Build.props`, comment |

⚠ **The trim analyser cannot see compiled XAML.** Avalonia's XAML compiler weaves IL after Roslyn;
only the publish in `trimcheck/` reads it.
