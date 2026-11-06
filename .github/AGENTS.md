# AGENTS.md — `.github/`

The workflows and the repository's GitHub settings. `ci.yml` runs every family on every pull request;
`release.yml` is Templates' canonical release plus the two local-feed steps; `repository.json` names
the required checks and the `families`. DiffView's `spike-injected-input.yml` is dispatch-only and has
never run.

## AppServices

The workflows and the repository's GitHub settings.

| File | What it is |
|---|---|
| `workflows/ci.yml` | On every push and pull request: `build` (build and test), `pack` (pack, then `scripts/assert-packages.cs`), `conventions` (`scripts/repo-conventions.cs check`), and `aot-linux` and `aot-windows` (`tests/EntryPointProbe` published with native AOT, then `EntryPointProbeTests` run against the binary) |
| `workflows/release.yml` | Publishes to nuget.org through trusted publishing, on a `v*.*.*` tag. Dispatched with the version blank, it only proves the credentials |
| `repository.json` | What this repository's GitHub settings vary by: description, topics, required checks, exemptions. `scripts/repo-conventions.cs` applies and checks it |
| `copilot-instructions.md` | A pointer to the root `AGENTS.md` for tools that look here |

### Rules

| Rule | Why | Guarded by |
|---|---|---|
| **The CI job names `build`, `pack`, `conventions`, `aot-linux` and `aot-windows` are required checks**, listed under `requiredChecks` in `repository.json`. Renaming or removing one means updating the list and running `apply` in the same change | A required check that no job reports blocks every pull request, and GitHub never says why | `scripts/repo-conventions.cs check` fails on the mismatch |
| **`release.yml` keeps its file name** | The trusted-publishing policy on nuget.org names this file, and the OIDC token is bound to the name. Renamed, every login fails | `docs/publishing.md`, policy field `Workflow File` |
| The release names each package it pushes and attaches, from `packages.push`, and never globs `*.nupkg` | A glob publishes whatever is in the folder, permanently | `PackagingTests.The_release_workflow_globs_nothing_and_publishes_what_is_declared`, `PackagingTests.The_release_workflow_never_names_a_private_package` |
| `release.yml` keeps its credential preflight: every build, push and release step is gated on `RELEASING` | A blank-version dispatch must prove the login without publishing | `PackagingTests.The_release_workflow_has_a_credential_preflight` |
| Every gate runs before `NuGet Login (Trusted Publishing)`, and the login stays immediately before `Push to NuGet.org` | Past the push nothing can be undone, and the login's token is short-lived | `release.yml`, step order and comments |
| `release.yml` holds `id-token: write` and `contents: write`, nothing broader | `id-token: write` is what replaces a stored API key; `contents: write` creates the GitHub Release | `release.yml`, `permissions` |
| The `conventions` job holds `contents: read` only | `check` reads settings through the workflow's token and must not be able to change them | `ci.yml`, `permissions` |

⚠ `PackagingTests` drops comment lines before searching `release.yml`, so a comment may explain the
glob it forbids; a command line may not contain one.

## ScopedEditors

The workflows and the repository's GitHub settings.

| File | What it is |
|---|---|
| `workflows/ci.yml` | On every push and pull request: `build` (build and test), `pack` (pack and `assert-packages.cs`), `trim` (the rooted trimmed publish in `trimcheck/` against its baseline) and `conventions` (`repo-conventions.cs check`) |
| `workflows/release.yml` | Publishes to nuget.org through trusted publishing, on a `v*.*.*` tag. Dispatched with the version blank, it only proves the credentials |
| `repository.json` | What this repository's GitHub settings vary by: description, topics, required checks, exemptions. `scripts/repo-conventions.cs` applies and checks it |
| `copilot-instructions.md` | A pointer to the root `AGENTS.md` for tools that look here |

### Rules

| Rule | Why |
|---|---|
| **The CI job names `build`, `pack`, `trim` and `conventions` are required checks**, listed under `requiredChecks` in `repository.json`. Renaming or removing one means updating the list and running `apply` in the same change | A required check that no job reports blocks every pull request, and GitHub never says why. `check` fails on the mismatch |
| **`release.yml` keeps its file name** | The trusted-publishing policy on nuget.org names this file, and the OIDC token is bound to the name. Renamed, every login fails |
| The credential preflight stays inside `release.yml`, gated on `RELEASING` | A preflight in another file could never match the policy; the gate is what stops a preflight publishing |
| The release names each package it pushes and attaches, from `packages.push` | A glob publishes whatever is in the folder, permanently. `PackagingTests.The_release_workflow_globs_nothing_and_publishes_what_is_declared` reads the workflow with comments removed |
| `release.yml` holds `id-token: write` and nothing broader than it needs | That permission is what replaces a stored API key |
| `NuGet Login (Trusted Publishing)` stays immediately before `Push to NuGet.org` | The token it returns is short-lived |
| The `trim` job's `Publish trimmed` step keeps `shell: bash` | It is what enables `pipefail`, so a failed publish is not hidden by `tee` |
| `description` in `repository.json` names the package ids as they are published, `.Avalonia` included | It is the GitHub description; `check` fails on an empty one |

## DiffView

The workflows and the repository's GitHub settings. What governs `main` is the root `AGENTS.md`'s §8.

| File | What it is |
|---|---|
| `workflows/ci.yml` | On every push and pull request: `Build & Test` on Linux, Windows and macOS, `Culture Leg (de-DE)`, `Trim Check (Release linux-x64)` and `conventions`. Each job fetches the reference sources and packs ClaudeForge's diagnostics into the local feed before it restores, because the demo restores from that feed |
| `workflows/release.yml` | Packs and publishes both packages on a `v*.*.*` tag, at the version the tag names. Dispatched with the preflight box ticked and no version, it reaches the NuGet login and stops |
| `workflows/spike-injected-input.yml` | Open item 16's spike, dispatch-only and shelved: it has never run |
| `repository.json` | What this repository's GitHub settings vary by: the description, the topics, the checks `main` requires, and exemptions. `scripts/repo-conventions.cs` checks it, and its `apply` writes it to GitHub |
| `copilot-instructions.md` | A pointer to the root `AGENTS.md`, for the tools that look here |

### Rules

| Rule | Why | Guarded by |
|---|---|---|
| **A job's name is a required check.** Renaming or removing a job `repository.json` lists under `requiredChecks` changes that list and runs `repo-conventions apply` in the same change | A required check no job reports blocks every pull request, and GitHub never says why | `repo-conventions check`, which fails on a required check no job reports |
| `conventions` joins `requiredChecks` only once it is green, and `apply` runs again then | Required while red, it would block every pull request | `docs/repository-conventions.md` in Bennewitz.Ninja.Templates, *Bringing an existing repository in* |
| The release pushes the packages it names, never a `*.nupkg` glob | A glob publishes whatever the build produced, permanently | `PackagingTests.The_release_workflow_names_the_packages_it_pushes`; `PackagesPushTests.The_file_names_exactly_what_the_solution_packs_and_the_release_publishes` holds what it names to `packages.push` |
| The version is the tag's, carried by `/p:Version=` into the build **and** the pack | `dotnet pack --no-build` reuses the build's output, so a version given to one alone packs the wrong thing silently | `PackagingTests.The_release_workflow_carries_the_tag_version_into_build_and_pack` |
| A preflight publishes nothing: the two steps that reach outside the job carry `if: ${{ !inputs.preflight }}` | An empty `/p:Version=` is MSBuild's route to `1.0.0`, and a published version is permanent | `PackagingTests.A_preflight_dispatch_reaches_the_credentials_and_no_further` |
| Nothing is released from a repository the family's conventions reject: the release runs `repo-conventions check --release` once the local feed is packed and before it builds | A package page links back here, so the repository's own documents are part of what ships | the step itself |
| `release.yml` keeps its file name | nuget.org's trusted-publishing policy names the workflow file, and the OIDC token is bound to it | the file's own header |
| CI runs `scripts/mutate-gates.cs --guards` on Linux | An `inert-at-pin` marker expires when its pin moves, and a pin bump otherwise lands green | the step itself; root `AGENTS.md` §5 |
