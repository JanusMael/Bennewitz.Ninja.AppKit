# 00005 — A tooltip on every input control

> Status: **approved 2026-11-16**. Supersedes nothing. The maintainer's choice of 2026-11-13,
> made through `choices` in the Templates session, which relayed it (Templates' `PROGRESS.md`, in
> JanusMael/Bennewitz.Ninja.Templates#98).

XamlQuality `2026.4.1111` adds BNXQ1011: every input control written in markup declares a tooltip,
saying what it does to the person pointing at it, which neither its automation name nor its
`AutomationId` shows them. The maintainer chose to pin it and adopt the rule over AppKit's own markup,
with the tooltips it finds missing added, `FatalErrorDialog`'s buttons among them. Measured the same
day: the rule can see only ScopedEditors, since AppServices has no markup.

## The requirement

| # | From the request |
|---|---|
| a | XamlQuality pinned at `2026.4.1111` |
| b | BNXQ1011 adopted over AppKit's own markup, as a test |
| c | The tooltips it finds missing added, measured |
| d | `FatalErrorDialog`'s buttons among them |
| e | In the next AppServices release |

## What exists

Measured on 2026-11-13 in a worktree at `e890fde`, unless it says read.

| Source | What it shows |
|---|---|
| The pin raised alone | Changes nothing: no warning, and 649 tests, 634 passed and 15 skipped, before and after. Between the two tags XamlQuality's rules gained only `InputTooltipRule` and its helper (read, the tags compared) |
| BNXQ1011, `InputTooltipRule` (read at its tag) | An input control is an element named `AutoCompleteBox`, `Button`, `CalendarDatePicker`, `CheckBox`, `ComboBox`, `DatePicker`, `DropDownButton`, `NumericUpDown`, `RadioButton`, `RepeatButton`, `Slider`, `SplitButton`, `TextBox`, `TimePicker`, `ToggleButton`, `ToggleSplitButton` or `ToggleSwitch`, templates included. A tooltip is `ToolTip.Tip` or one of its kin, as an attribute with any value that is not blank, a binding included, or as a property element. A control without one is skipped rather than reported when a style's tooltip setter may reach it. It reads markup only: "A control built in code, or a tooltip set in code, is not seen" |
| ScopedEditors' markup | 12 input controls inspected, 10 reported, none skipped, all in `Controls/PropertyEditorWrapper.axaml`. Six are value inputs: the `CheckBox` (line 245), the `TextBox` (261), the path's `TextBox` (274), the strict enum's `ComboBox` (294), the free-form enum's `AutoCompleteBox` (311) and the `NumericUpDown` (345). Four are fixed chrome: Browse (280), the list's × (367), its new-item `TextBox` (388) and Add (393). The Reset and ▾ buttons already have tooltips, from `WrapperStrings` |
| The value inputs (read) | Their automation names bind the property's `DisplayName` and `Description`, and `Description` may be null; a tooltip bound to a null renders nothing and still passes the rule. On a locked row the editor is disabled, and a disabled control shows its tooltip only with `ToolTip.ShowOnDisabled` |
| `WrapperStrings` (read) | The package's fixed strings, `TipResetToInherited` and `TipShowSuggestions` among them, each a public static property a host can override. `WrapperStringsNeutralityTests` and `EveryKeyIsOverridable` find a new one by reflection |
| AppServices | No markup at all, so BNXQ1011 inspects nothing there. `FatalErrorDialog` and `NonFatalNoticeDialog`, built in C#, each have Close, Copy to Clipboard and a read-only details `TextBox`, none with a tooltip, and `DialogAccessibilityTests` walks both on the headless platform. `AvaloniaDialogService`'s dialogs (eleven buttons and a `TextBox`, none with a tooltip) are built only when the app has a desktop main window, so no test reaches them, for automation names either |

## Decisions

| # | Decision | Why |
|---|---|---|
| 1 | **XamlQuality pinned at `2026.4.1111`**, the one pin both families' tests read | Requirement a. Test-only, so no consumer's floor moves, and measured to change nothing on its own |
| 2 | **BNXQ1011 runs over ScopedEditors' markup**, beside its other markup rules, through `PackageSources.Markup()`: 12 inspected, nothing skipped, nothing found | Requirement b, as far as the rule reaches. The count pinned, as the BNXQ1002 test pins its twelve, so a new input control raises it; an empty `Skipped`, so a style's setter cannot hide a control the rule did not decide |
| 3 | **A value input's tooltip is its property's `Description`, or its `DisplayName` when it has none, and shows on a locked row too** (`ToolTip.ShowOnDisabled`). The choice is made in the markup, by a converter of the Avalonia package's, so `ScopedEditors.ViewModels` is unchanged | What the input sets is what the person pointing at it wants to know. A tooltip bound to a null description would render nothing and still pass the rule. A locked row is where someone points to ask what it is |
| 4 | **The fixed chrome's tooltips are four new `WrapperStrings` keys**, `TipBrowse`, `TipRemoveItem`, `TipNewItem` and `TipAddItem`, in neutral English beside the existing `Tip` keys | A host overrides them as it does every other key, and the neutrality and override tests cover them by reflection. New public members, an addition only |
| 5 | **`FatalErrorDialog` and `NonFatalNoticeDialog` give each of their three controls a tooltip**, guarded by `DialogAccessibilityTests`' walker: every control it requires a name of has a tooltip that is not blank | Requirement d. BNXQ1011 cannot see a control built in C#, and the walker already visits these. The walker's own definition of an interactive control stays the one list |
| 6 | **`AvaloniaDialogService`'s dialogs are left out**, and `PROGRESS.md` names the gap | No test can build them today, for their names either; reaching them means building their windows where a test can, a change of its own, for a plan of its own if the maintainer wants it |
| 7 | **Both families release**, each on the maintainer's word at the day's version: ScopedEditors for its markup and keys, AppServices for its two dialogs | The rule's ten findings are ScopedEditors'; the request named only the AppServices release |

### Dismissed

- **BNXQ1011 over AppServices.** There is no markup to scan, so it would inspect nothing.
- **`{Binding Description}` alone.** It renders nothing for a property without a description, which
  the rule cannot catch.
- **The property's path as the fallback.** A key, not words, and the name's own tooltip shows it
  already.
- **A copy of BNXQ1011's list of input types in the walker.** A second list rots; the walker's
  definition already covers these dialogs.
- **The pin alone.** The maintainer chose the rule as well (2026-11-13).

## Scope

**In:** decisions 1 to 7 and their tests; the doc comments on the new keys; `docs/scopededitors.md`, an
"Upgrading from 2026.4.1110" note: the inputs show tooltips, and four new keys a host may override;
the `AGENTS.md` rows for BNXQ1011 and the dialogs' tooltips;
`tests/AGENTS.md`, whose ScopedEditors section still calls its project the repository's only test
project and leaves BNXQ1009 and BNXQ1010 out of its accessibility row; `PROGRESS.md`; a release of each
family; and telling Templates the AppServices version.

**Out:** `AvaloniaDialogService`'s dialogs (decision 6); XamlQuality's rules still unadopted; the
templates.

## Steps

| # | Step | Verified by |
|---|---|---|
| 1 | This plan, committed on its own once approved | CI green |
| 2 | The pin; the BNXQ1011 test; the value inputs' tooltips (decision 3) and the four keys (decision 4) in `PropertyEditorWrapper.axaml` | The BNXQ1011 test: 12 inspected, nothing skipped, nothing found; seen red with one tooltip removed, and again with that tooltip removed and a style's tooltip setter for its type planted, which moves the control into `Skipped`. A rendered row reads each value input's tooltip, with a description and without one, on an editable row and a locked one. The `WrapperStrings` tests pass with the new keys. The whole suite |
| 3 | The two dialogs' tooltips, and the walker's assertion | Seen red with one tooltip removed. Package validation of both families against their last releases reports no breaking change |
| 4 | The docs, the `AGENTS.md` rows and `PROGRESS.md` | `repo-conventions check` conforms; CI green |
| 5 | Release ScopedEditors and AppServices, each at the day's version, on the maintainer's word, through `docs/publishing.md` | Every id of each in the nuget.org flat container and restored from nuget.org alone, then `PROGRESS.md`'s Published rows |
| 6 | Tell Templates the AppServices version, beside `00004`'s line if one release carries both | Told |
