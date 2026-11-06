# Bennewitz.Ninja.AppKit

Building blocks for .NET desktop apps: the services an app runs on, the editors it shows, and the
diff controls it hosts. Each family has its own packages, its own version line and its own releases.

| Family | What it is | Packages | Its README |
|---|---|---|---|
| AppServices | Shell launching, environment probing, sharing, dialogs, logging and one entry point for every kind of app | [`Bennewitz.Ninja.AppServices`](https://www.nuget.org/packages/Bennewitz.Ninja.AppServices), [`.Abstractions`](https://www.nuget.org/packages/Bennewitz.Ninja.AppServices.Abstractions), [`.Logging`](https://www.nuget.org/packages/Bennewitz.Ninja.AppServices.Logging), [`.Avalonia`](https://www.nuget.org/packages/Bennewitz.Ninja.AppServices.Avalonia), [`.EntryPoint`](https://www.nuget.org/packages/Bennewitz.Ninja.AppServices.EntryPoint) | [docs/appservices.md](docs/appservices.md) |
| ScopedEditors | Schema-driven property editors for data set at more than one scope | [`Bennewitz.Ninja.ScopedEditors.Abstractions`](https://www.nuget.org/packages/Bennewitz.Ninja.ScopedEditors.Abstractions), [`.ViewModels`](https://www.nuget.org/packages/Bennewitz.Ninja.ScopedEditors.ViewModels), [`.Avalonia`](https://www.nuget.org/packages/Bennewitz.Ninja.ScopedEditors.Avalonia) | [docs/scopededitors.md](docs/scopededitors.md) |
| DiffView | Side-by-side and unified diff controls for Avalonia 12 | `Bennewitz.Ninja.DiffView.Core`, `Bennewitz.Ninja.DiffView.Avalonia`, not yet released | [docs/diffview.md](docs/diffview.md) |

Each family's README is its packages' nuget.org page.

## AppServices

Application services behind contracts a test can fake without referencing a UI toolkit. The launcher
and the share service report what actually happened, as a `LaunchResult` or a `ShareOutcome`, rather
than completing the same way whether or not anything did. `AppMain` is the whole of an app's `Main`,
console, web or desktop: exit codes a script can read, a fatal report on stderr, Ctrl+C, `--version`,
and the app's log flushed on every exit.

```bash
dotnet add package Bennewitz.Ninja.AppServices.Avalonia
```

In an Avalonia application, that one package brings the others with it. Any app takes
`Bennewitz.Ninja.AppServices.EntryPoint` for its `Main`; it references nothing else, so a native AOT
app can take it too.

## ScopedEditors

Property editors for structured data whose values can be set at more than one scope, where one
scope overrides another. The model records which scope each value came from and annotates it with a
severity; the view-models present it without referencing any UI framework; the Avalonia package
draws it.

```bash
dotnet add package Bennewitz.Ninja.ScopedEditors.Avalonia
```

## DiffView

Text diff controls for Avalonia 12: row-aligned panes on AvaloniaEdit, line and word-level
highlighting, change navigation, minimap, connectors, find across either or both panes, syntax
highlighting, and a unified view over the same model.

**Putting the control into your own application? Read [docs/diffview.md](docs/diffview.md).** It is
the guide for consumers: install, quickstart, theming, the extension points, what the dependency
costs you and where the control stops. Working *on* DiffView rather than *with* it,
[docs/diffview-development.md](docs/diffview-development.md) has the building, the theme audit and
the parts reference.

The eight shipped locales are machine-generated and have **not** been read by a native speaker. That
does not gate a release: blocking a publish on eight volunteers has no end date, and
`DiffViewStrings.Localization` lets a host outrank the library with a resolver of its own.
`docs/locale-review/` holds one document per locale for exactly that review.

## Releases

Each family releases alone, from a tag of its own: `appservices/vYYYY.Q.MMDD`,
`scopededitors/vYYYY.Q.MMDD` or `diffview/vYYYY.Q.MMDD`. The version is the tag's, and a release
publishes only that family's packages, to nuget.org through trusted publishing; no API key exists in
this repository. AppServices' releases before it was joined by the other two carry plain
`vYYYY.Q.MMDD` tags.

[docs/publishing.md](docs/publishing.md) is the runbook: the one-time setup, the preflight, releasing,
and checking the result against the feed.

## Contributing

[AGENTS.md](AGENTS.md) holds the invariants, the commands and the checklists, for people and agents
alike, and [PROGRESS.md](PROGRESS.md) the work in hand. The first build needs a local feed, filled by
two scripts:

```bash
dotnet run scripts/fetch-reference.cs
```

```bash
dotnet run scripts/pack-diagnostics.cs
```

## Licence

MIT. See [LICENSE](LICENSE).
