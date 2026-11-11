# AGENTS.md — `tests/`

The families' test projects, and the apps AppServices' entry-point tests run.
`tests/Directory.Build.props` makes each test project an xUnit v3 test executable on
Microsoft.Testing.Platform that is never packed. A test that scans shipped projects
scans its own family's, the `src/` folders named for it.

## AppServices

The test project `AppServices.Tests`, referencing every shipped project. `tests/Directory.Build.props`
makes it an xUnit v3 test executable that is never packed. Beside it, `EntryPointProbe` and
`EntryPointWebProbe` are apps, not tests: each has a `Directory.Build.props` of its own, which
imports the root one in place of this directory's and sets `IsPackable` to false.

| Folder | Classes | What they cover |
|---|---|---|
| `Architecture/` | `LayeringTests`, `AssemblyQualityTests`, `PackageMetadataTests` | What each project may reference, the family's AssemblyQuality rules over every shipped assembly, and the metadata and trimmable mark every package carries |
| `Packaging/` | `PackagingTests` | Every packable project is classified in `packages.push` or `packages.local`, and the release workflow names what it publishes |
| `Shell/`, `Share/`, root | `ShellLauncherWindowsTerminalTests`, `DefaultShareServiceTests`, `LaunchResultTests`, `MovedSourceSmokeTests` | The launcher, the share service's reported `ShareOutcome`, `LaunchResult`, and the environment provider and dialog vocabulary |
| `Logging/`, `AvaloniaUI/` | `BucketedRollingFileSinkTests`, `SerilogAvaloniaSinkTests`, `AvaloniaDiagnosticsHookTests`, `AvaloniaDiagnosticsEntryPointTests`, `UiThreadExceptionHandlerTests` | Bucket arithmetic and retention, and each line written at its own level; the Avalonia-to-Serilog level mapping; the `ConfigureLogger` and `EventListener` hooks, a minimum per source among them; what `EntryPointOptions` does with a failure, the fatal log and then the native dialog, and the filter it passes on; and the UI-thread handler on a headless dispatcher: what it logs, shows, counts and leaves alone |
| `Dialogs/`, `Headless/` | `DialogAccessibilityTests`, `NativeErrorDialogTests`, `HeadlessSessionTests`, `HeadlessTestApp` | Every interactive control in the C#-built dialogs has an automation name, built on a headless Avalonia platform; the native dialog never throws |
| `EntryPoint/` | `AppMainTests`, `EntryPointProbeTests`, `EntryPointWebProbeTests` | `AppMain` in process: every exit code, both streams, `--version`, the hooks, and what the benign filter keeps off stderr. The console probe as a child process, and against its native AOT binary when `ENTRYPOINT_PROBE_NATIVE` names one. The web probe under `WebApplicationFactory`, and stopped on `HostBuilt` as a design-time tool stops an app |
| `BenignExceptions/` | `BenignExceptionTable`, `BenignExceptionRuleTests` | The benign rule (`plans/00002`, decision 3) as one table of cases, and both of its copies, EntryPoint's and the Avalonia package's, held to every row |

### Rules

| Rule | Why | Guarded by |
|---|---|---|
| Tests run on Microsoft.Testing.Platform | `dotnet test` takes `--solution` and rejects VSTest-only switches | `global.json`, `test.runner` |
| `tests/Directory.Build.props` imports the root props explicitly | Without it the test project silently loses the root's target framework and nullable settings | the import line itself |
| The assembly runs serially | Some tests change process-wide static state (`AvaloniaDiagnostics`, Serilog's `Log.Logger`, `NativeErrorDialog.SuppressForTests`), and the headless tests share one session and dispatcher | `Parallelization.cs`; `HeadlessSessionTests` guards the one-session premise |
| A test that changes static state restores it in `Dispose` | The next test inherits it | `AvaloniaDiagnosticsHookTests` (`AvaloniaDiagnostics.ResetForTests`, which also takes the UI-thread handler off its dispatcher and resets its gate, its counts and its dialog seam), `NativeErrorDialogTests` (`NativeErrorDialog.Reset`) |
| `BenignExceptionTable` is the one table of the benign rule: every test of either copy, and of what each drives, reads its rows, and none keeps cases of its own | The two copies exist because the packages may not share an internal; held to two tables, they would drift apart unseen | `BenignExceptionRuleTests.The_table_holds_every_case_of_the_rule`, which pins the row count |
| A UI-thread handler test installs, `Post`s a throwing job, runs `RunJobs()` and asserts inside one `Session.Dispatch`, with a fresh exception per throw, and at Debug wherever it asserts a Debug line | Each dispatch gets a dispatcher of its own; only a posted job's exception reaches `UnhandledException`; the dispatcher offers an exception object once; the default minimum, Information, drops a Debug line | `UiThreadExceptionHandlerTests` remarks |
| An unobserved-exception test of the benign filter collects garbage before its run hooks in | A task an earlier test abandoned would otherwise reach this run's hook and write to its stderr | `AppMainTests` remarks |
| Headless isolation is per test, and there is no warm-up | Each dispatch builds a fresh application, so a warm-up changes nothing. Switching to `AvaloniaTestIsolation` per assembly changes what every test shares and needs its own evidence first | `HeadlessSessionTests` remarks |
| A share test never passes text without a URI on Windows or macOS | That path runs the real `clip.exe` or `pbcopy` and overwrites the developer's clipboard; every other path goes through the launcher injected into `DefaultShareService` | `DefaultShareServiceTests` remarks |
| A platform-bound test skips with `Assert.Skip` or `Assert.SkipUnless`, never passes silently | A skip is visible in the report; an early return reads as a pass | `ShellLauncherWindowsTerminalTests`, whose launching tests carry `[Trait("Category", "Integration.WT")]` or `"Diagnostic.WT"`; `LaunchResultTests` |
| `LayeringTests.Tiers` and `LayeringTests.ForeignFamilies` are the one table of what may reference what | `AssemblyQualityTests` reads them for BNAQ1003; a copy is the list that silently rots | `AssemblyQualityTests.BNAQ1003_no_assembly_references_what_its_tier_forbids` |
| The layering guards keep both the csproj check and the reflection check | The compiler omits an unused reference from the assembly, so a declared-but-unused bad reference is invisible to reflection | `LayeringTests` remarks |
| The vacuity guards stay | Every other assertion iterates a list, and an empty list passes all of them; a rule that could not load part of what it was given reports clean on the rest | `LayeringTests.Every_tier_named_here_actually_exists`, `PackageMetadataTests.There_is_something_to_check`, `AssemblyQualityTests.Every_shipped_assembly_is_in_the_scan`, each rule's `Inspected > 0` and empty `Skipped` |
| `DialogAccessibilityTests` pins the number of interactive controls each dialog has | A new control must bump it; a lower count means the walker lost something | `DialogAccessibilityTests` remarks |
| After adding, removing or renaming a project, run `repo-conventions grants` | The grants are generated from every project's assembly name, so until the list is regenerated a renamed assembly receives none, and `check` fails | `scripts/repo-conventions.cs check` |
| The console probe is built and run, never referenced for its assembly | Its `Program` would sit beside the web probe's, which `WebApplicationFactory<Program>` needs | `ReferenceOutputAssembly="false"` in `AppServices.Tests.csproj` |
| `WebApplicationFactory` is not the proof that `HostAbortedException` passes through | It resolves the host with `stopApplication: false` and never throws it; only a stop on `HostBuilt`, as a design-time tool makes, does | `EntryPointWebProbeTests` remarks |
| A test that needs the native probe skips, visibly, without `ENTRYPOINT_PROBE_NATIVE` | The native binary exists only where CI's aot jobs publish it | `EntryPointProbeTests` |
| A signal test asserts the probe's `probe: flushed` marker, never its exit code alone | .NET reports a child killed by a signal as 128 plus the signal's number, so a probe that SIGINT killed outright reads 130, exactly as a handled Ctrl+C does | `EntryPointProbeTests` remarks |

⛔ **Never weaken an architecture or packaging test to make it pass.** When one fails, the project,
the package list or the workflow is wrong, not the test.

## ScopedEditors

`ScopedEditors.Tests`, the repository's only test project. `tests/Directory.Build.props` makes it an
xUnit v3 test executable that is never packed. It references all of `src/`, `Avalonia.Headless`,
`Bennewitz.Ninja.AssemblyQuality` and `Bennewitz.Ninja.XamlQuality`.

| Folder | What it covers |
|---|---|
| `Architecture/` | `LayeringTests`, `AssemblyQualityTests` (BNAQ1001–BNAQ1004 over every shipped assembly), `PackageMetadataTests`, and the markup guards `ThemeResourceIntegrityTests`, `DangerSurfaceMarkupTests`, `SeverityGlyphFontSizeMarkupTests` |
| `Accessibility/` | XamlQuality's `BNXQ1002` (`AxamlAccessibilityCoverageTests`), `BNXQ1001` (`ExpanderAutomationNameTests`) and `BNXQ1007` (`AxamlAutomationIdCoverageTests`) over the package's markup. The ids' values are read off a rendered row in `Controls/PropertyEditorWrapperAutomationIdTests` |
| `Packaging/` | `PackagingTests`: the package lists and the release workflow |
| `Assets/` | `BundledFontTests`: text laid out in every bundled face by its documented `avares://` URI |
| `Themes/`, `Controls/`, `Converters/`, `Behaviors/`, `Helpers/`, `Localization/`, `ViewModels/` | The suite ported with the code, organised by what it tests |
| `Headless/` | `HeadlessTestApp`, which loads `Themes/SemiBundle.axaml`, and `HeadlessSessionTests` |
| the project root | `Parallelization.cs`, `MovedSourceSmokeTests`, `ScaffoldingSanityTests` |
| `Support/`, `Fakes/` | `PackageSources` (the one definition of what the markup guards read), `MessageAssert`, and fakes of the `.Abstractions` interfaces |

### Rules

| Rule | Why | Guarded by |
|---|---|---|
| Tests run on Microsoft.Testing.Platform | `dotnet test` takes `--solution` and rejects VSTest-only switches | `global.json`, `test.runner` |
| `tests/Directory.Build.props` imports the root props explicitly | Without it the test project silently loses the root's target framework and nullable settings | the import line itself |
| Tests run one at a time | There is one headless Avalonia dispatcher per assembly, and it must not be driven concurrently | `Parallelization.cs`; `HeadlessSessionTests.GetOrStartForAssembly_ReturnsTheSameSessionEveryTime` |
| A headless test dispatches a **synchronous** body through `HeadlessUnitTestSession.GetOrStartForAssembly` | An `async` body returns a `Task<Task>` nothing awaits, so the test cannot fail | `HeadlessTestApp`, summary |
| No warm-up dispatch and no shared application across tests without `[AvaloniaTestIsolation]` evidence | With no isolation attribute every dispatch builds a fresh application, so a warm-up proves nothing | `HeadlessSessionTests`, remarks |
| Every scan asserts it inspected something before asserting it found nothing, or says beside the assertion why zero is true; an AssemblyQuality rule that loads assemblies also asserts `Skipped` is empty | Zero findings from a scan that saw nothing, or only part of what it was given, reads as a clean codebase | `AssemblyQualityTests.Every_shipped_assembly_is_in_the_scan` and its `Skipped` assertions; `PackageSources.Markup`; the `Inspected` assertions in each markup guard |
| The markup guards read the project through `PackageSources`, with XML comments stripped | One home for the scope; a binding named only in a comment is not a binding | `PackageSources.Project`, `PackageSources.MarkupText` |
| `LayeringTests.Tiers` is the single table of what each project may reach | `AssemblyQualityTests.BNAQ1003_no_assembly_references_what_its_tier_forbids` reads it too; a copy would rot | `LayeringTests.Every_tier_named_here_actually_exists` |
| A new `LE.*` key used only by hosts goes in `ThemeResourceIntegrityTests.KeysKeptForHosts` with its reason | The list is the only exemption from the dead-token check, and it checks itself | `Every_key_kept_for_hosts_is_still_declared_and_still_unused_here` |

⛔ **Never weaken a packaging, layering or markup guard to make it pass.** When one fails, the
package list, the project or the markup is wrong, not the test. A changed guard is proven by
planting the defect it exists for, watching exactly that test fail, and removing the defect.
