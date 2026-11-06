# AGENTS.md — `docs/`

Each family's README, `appservices.md`, `scopededitors.md` and `diffview.md`, each packed as that
family's packages' `README.md`, so its links are absolute; `publishing.md`, the release runbook; and
DiffView's own documents, among them `diffview-development.md` and `diffview-progress.md`.

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

## DiffView

Documentation for readers outside this repository, and the documents tools write.

| File | What it is | Written by |
|---|---|---|
| `diffview.md` | The hosting guide, and the `PackageReadmeFile` of both packages, so nuget.org shows it | hand |
| `mutation-proofs.md` | The method `scripts/mutate-behaviour.cs` proves a behaviour test able to fail by | hand |
| `ui-automation-notes.md` | What this repository measured about driving a desktop UI that has not moved to Inspections' documents | hand |
| `theme-audit.md` | The theme audit's report | `theme-audit report`, from `theme-audit.json` |
| `locale-review/<culture>.md` | A review packet per shipped locale, for a native speaker | `scripts/gen-locale-review.cs` |
| `template-parts.md` | Every control the library defines, its template parts, their `AutomationId`s and what looks each up | `TemplatePartReferenceTests`, through `scripts/gen-template-parts` |

### Rules

| Rule | Why | Guarded by |
|---|---|---|
| **A generated document is never edited by hand**: a correction goes into what it is generated from — a `.resx`, the themes and `theme-audit.json`, or the code — and the document is regenerated | Two copies of one fact with no rule about which wins | `LocaleReviewTests.Every_committed_review_document_describes_the_strings_it_claims_to`, `ReferenceAuditTests.The_committed_report_equals_a_fresh_run`, `TemplatePartReferenceTests.The_committed_reference_equals_a_fresh_reading` |
| A gate that disagrees with a document here writes `<name>.received.md` beside it; CI uploads that when a job fails, and `.gitignore` keeps it out of a commit | The failure otherwise names a file nobody outside the runner can read | `.github/workflows/ci.yml`, *Upload what the gates rejected* |
| The hosting guide's `AutomationId` table is the fixture's, both ways | A host scripts against the ids it reads there | `HostingGuideTests.The_guides_id_table_names_exactly_the_ids_the_fixture_pins` |
| The hosting guide states the locale caveat: the translations are machine-generated and unreviewed | A consumer meets it there, and on nuget.org | `LocaleCaveatTests.Every_place_a_consumer_meets_the_caveat_states_it` |
| Every `§N` a document here cites names a section of the root `AGENTS.md` that exists, and every test this file and the hosting guide name exists | A citation is evidence only while it resolves | `DocumentationCitationTests` |
