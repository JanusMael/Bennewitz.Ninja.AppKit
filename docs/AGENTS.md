# AGENTS.md — `docs/`

Documents for humans. What an agent needs lives in the `AGENTS.md` files, not here. None of it is
packed; the packages carry the root `README.md` only.

| Document | What it is | Kept in step with |
|---|---|---|
| `publishing.md` | The release runbook: one-time trusted-publishing setup, the version rule, releasing, and verifying against the feed | `.github/workflows/release.yml`. A step renamed or reordered there is renamed or reordered here in the same change |

## Rules

| Rule | Why |
|---|---|
| `publishing.md` names `release.yml` as the trusted-publishing policy's workflow file and `Bennewitz.Ninja.ScopedEditors` as its repository | The policy on nuget.org matches those literally; the document is where a maintainer copies them from |
| Consumer-facing upgrade notes, such as the `avares://` and namespace changes, go in the root `README.md`, not here | The README is the nuget.org description, so it is what a consumer reads before upgrading |
| The history of the package split and the renames lives in Bennewitz.Ninja.Templates (`docs/layered-editors-renames.md`, `docs/layered-editors-package-split.md`) and is linked rather than copied | Two copies of a record eventually disagree |
