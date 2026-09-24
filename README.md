# Bennewitz.Ninja.ScopedEditors

Schema-driven property editors for structured data whose values can be set at more than one scope,
where one scope overrides another. The model records which scope each value came from and annotates
it with a severity; the view-models present it without referencing any UI framework; the Avalonia
package draws it.

| Package | What it is |
|---|---|
| `Bennewitz.Ninja.ScopedEditors.Abstractions` | The scope and override model: editor schemas, scopes, values, workspaces, and the severity model that annotates them. Framework-free. |
| `Bennewitz.Ninja.ScopedEditors.ViewModels` | Presentation logic with no UI-framework reference at all, so an editor can be built off the UI thread. |
| `Bennewitz.Ninja.ScopedEditors.Avalonia` | Avalonia views, converters and behaviours, two bundled monospace fonts, and an opt-in Semi.Avalonia theme bundle. |

## Install

```bash
dotnet add package Bennewitz.Ninja.ScopedEditors.Avalonia
```

That brings the other two with it. Reference `.Abstractions` or `.ViewModels` on their own from code
that must not see a UI framework.

⚠ `.AvaloniaUI` depends on Semi.Avalonia and Semi.Avalonia.DataGrid whether or not you use the Semi
theme. It is the only package in the family that does.

## Using the Avalonia package

Everything is reached by `avares://` URIs, and an `avares://` URI names the **assembly**.

⚠ **The package and the assembly inside it are named differently.** The package is
`Bennewitz.Ninja.ScopedEditors.Avalonia`; the assembly is `ScopedEditors.AvaloniaUI`, and so are the
namespaces, `Bennewitz.Ninja.ScopedEditors.AvaloniaUI.*`. A URI written from the package name,
`avares://ScopedEditors.Avalonia/…`, names an assembly the package no longer contains.

**With Semi.Avalonia**, merge the one bundle. It carries the Semi theme, the editors' colour tokens,
and screen-reader names for control-template parts that ship without one:

```xml
<Application.Styles>
    <StyleInclude Source="avares://ScopedEditors.AvaloniaUI/Themes/SemiBundle.axaml" />
</Application.Styles>
```

**With any other theme**, include the two parts that are not Semi's:

```xml
<Application.Styles>
    <FluentTheme />
    <StyleInclude Source="avares://ScopedEditors.AvaloniaUI/Themes/EditorColors.axaml" />
    <StyleInclude Source="avares://ScopedEditors.AvaloniaUI/Themes/AccessibilityNames.axaml" />
</Application.Styles>
```

Without `EditorColors.axaml`, colours looked up from code fall back to built-in defaults, but the
accent badge binds its colours in markup and silently loses its tint. Without
`AccessibilityNames.axaml`, the spin buttons inside a `NumericUpDown` are left unnamed for a screen
reader. Any `LE.*` colour token can be overridden from your own `Application.Resources` before the
first render; a per-theme-variant token belongs in your `ThemeDictionaries`.

**Fonts.** Two families are bundled, so monospaced text draws the same face on every platform:

| Family | URI | Weights |
|---|---|---|
| JetBrains Mono NL | `avares://ScopedEditors.AvaloniaUI/Assets/Fonts#JetBrains Mono NL` | 400, 600, 700 |
| JetBrains Mono | `avares://ScopedEditors.AvaloniaUI/Assets/Fonts#JetBrains Mono` | 400, 700 |

Prefer NL, the no-ligature cut, wherever the text is literal — a path, a command, a config value. A
ligature draws `!=` or `->` as one glyph, so what the user reads stops matching the file. Ask only for
the weights listed: any other resolves silently to the nearest one. Both families are OFL 1.1, and the
licence travels in the assembly beside them.

## Upgrading from 2026.3.923

The package keeps its id. Inside it, `2026.3.924` renames the assembly and its namespaces from
`.Avalonia` to `.AvaloniaUI`, because a namespace segment named `Avalonia` shadows Avalonia's own
root namespace. For a consumer, two things change:

- the namespaces: `Bennewitz.Ninja.ScopedEditors.Avalonia.*` → `Bennewitz.Ninja.ScopedEditors.AvaloniaUI.*`,
  in C# `using` directives and XAML `xmlns` declarations alike;
- every `avares://ScopedEditors.Avalonia/…` URI → `avares://ScopedEditors.AvaloniaUI/…`.

⚠ **A stale URI is not silent, and it does not fail the same way everywhere.** A `StyleInclude` in
AXAML fails the build with `AVLN2000`. A `FontFamily` naming one family throws
`InvalidOperationException` the first time text in it is laid out, so a view nobody opens hides it. A
`FontFamily` with a fallback list draws the next face and reports nothing. Search for the old name
rather than waiting for one of those.

`2026.3.924` is also the first release whose assemblies are marked trimmable. CI publishes all three
trimmed, and the only trim warnings are `Avalonia.Controls.DataGrid`'s own.

`2026.3.924` requires **Avalonia 12.1.3 or later** and **Avalonia.Controls.DataGrid 12.1.2 or later**.
Avalonia 12.1.3 fixes UI Automation selection never reaching the client on Windows
([AvaloniaUI/Avalonia#22151](https://github.com/AvaloniaUI/Avalonia/pull/22151)); DataGrid has no
12.1.3, and 12.1.2 is its newest. A host that references either directly at an earlier version fails
restore with `NU1605`, a package downgrade, until it raises that reference.

## Releasing

See [docs/publishing.md](docs/publishing.md). The short version:

1. Set the `NUGET_USER` repository **variable** to your nuget.org **profile name**, not an email. A
   variable, not a secret: GitHub masks a secret in the log, which hides the one value that explains
   a 401.
2. Create **one** trusted-publishing policy whose glob patterns cover every id in
   [`packages.push`](packages.push) and match nothing in [`packages.local`](packages.local).
3. Run **Release** → *Run workflow* with the version **blank**. That logs in and stops, proving the
   credentials without publishing.
4. Tag `vYYYY.Q.MMDD` and push.

⛔ **One policy, never one per package id.** nuget.org mints one API key per token exchange, scoped
to one matching policy — so a second policy is never consulted and its package is rejected `403`
after the first has already published permanently.

## Licence

MIT. See [LICENSE](LICENSE).
