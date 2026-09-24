# AGENTS.md — `.github/`

The workflows and the repository's GitHub settings.

| File | What it is |
|---|---|
| `workflows/ci.yml` | On every push and pull request: `build` (build and test), `pack` (pack and `assert-packages.cs`), `trim` (the rooted trimmed publish in `trimcheck/` against its baseline) and `conventions` (`repo-conventions.cs check`) |
| `workflows/release.yml` | Publishes to nuget.org through trusted publishing, on a `v*.*.*` tag. Dispatched with the version blank, it only proves the credentials |
| `repository.json` | What this repository's GitHub settings vary by: description, topics, required checks, exemptions. `scripts/repo-conventions.cs` applies and checks it |
| `copilot-instructions.md` | A pointer to the root `AGENTS.md` for tools that look here |

## Rules

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
