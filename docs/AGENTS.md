# AGENTS.md — `docs/`

Each family's README, `appservices.md` and `scopededitors.md`, each packed as that family's packages'
`README.md`, so its links are absolute; and `publishing.md`, the release runbook.

## AppServices

Documents for humans. What an agent needs lives in the `AGENTS.md` files, not here. Nothing here is
packed; `README.md` at the root is what nuget.org shows.

| Document | What it is | Kept in step with |
|---|---|---|
| `publishing.md` | The release runbook: one-time trusted-publishing setup, the `YYYY.Q.MMDD` version rule, releasing, verifying against the nuget.org flat-container, and the failure table | `.github/workflows/release.yml`. A step renamed or reordered there is renamed or reordered here in the same change |

### Rules

| Rule | Why |
|---|---|
| The trusted-publishing policy in `publishing.md` stays one policy whose glob covers every id in `packages.push` | nuget.org scopes one token exchange to one policy, so a second policy's package is rejected `403` after the first has published permanently |
| `publishing.md` names `NUGET_USER` as a repository variable holding the nuget.org profile name | That is what `release.yml` reads (`vars.NUGET_USER`); a masked secret hides the value that explains a `401` |

## ScopedEditors

Documents for humans. What an agent needs lives in the `AGENTS.md` files, not here. None of it is
packed; the packages carry the root `README.md` only.

| Document | What it is | Kept in step with |
|---|---|---|
| `publishing.md` | The release runbook: one-time trusted-publishing setup, the version rule, releasing, and verifying against the feed | `.github/workflows/release.yml`. A step renamed or reordered there is renamed or reordered here in the same change |

### Rules

| Rule | Why |
|---|---|
| `publishing.md` names `release.yml` as the trusted-publishing policy's workflow file and `Bennewitz.Ninja.ScopedEditors` as its repository | The policy on nuget.org matches those literally; the document is where a maintainer copies them from |
| Consumer-facing upgrade notes, such as the `avares://` and namespace changes, go in the root `README.md`, not here | The README is the nuget.org description, so it is what a consumer reads before upgrading |
| The history of the package split and the renames lives in Bennewitz.Ninja.Templates (`docs/layered-editors-renames.md`, `docs/layered-editors-package-split.md`) and is linked rather than copied | Two copies of a record eventually disagree |
