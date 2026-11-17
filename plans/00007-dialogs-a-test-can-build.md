# 00007 — Dialogs a test can build

> Status: **approved 2026-11-17**. Supersedes nothing. The maintainer's choice of 2026-11-16, made
> while settling `plans/00006`: `AvaloniaDialogService`'s dialogs get a plan of their own, made buildable
> in tests and then given their colours, tooltips and names, and `plans/00006` waits for it.

`AvaloniaDialogService` shows six dialogs of its own, and no test builds any of them. Each is put
together inside the public method that shows it, which looks for the desktop's main window first,
returns without a dialog when there is none, and then waits on a modal loop no test can click into. So
nothing checks their names, their tooltips or their colours. Their colours are literals chosen for a
light window: on a dark one, link text reads 3.6:1 where text needs 4.5:1. Their code pane reads two
keys that only a host that knows of them declares. `FatalErrorDialog` and `NonFatalNoticeDialog` are
tested because each is a window class a test constructs. This plan gives every dialog that shape, then
gives the six names, tooltips and colours from keys the package declares, which `plans/00006` builds on.

## The requirement

| # | From the request |
|---|---|
| a | Every dialog the package builds can be built by a test, without a desktop lifetime or a main window. The maintainer asked on 2026-11-16 whether dialogs should be created in a more testable way |
| b | Every interactive control in them has a name and a tooltip |
| c | Their colours come from keys the package declares for Light and Dark, and follow a change of theme |
| d | It comes before `plans/00006`, whose pattern then takes in this plan's dictionary and its rule for windows |

## What exists

Read on 2026-11-16.

| Source | What it shows |
|---|---|
| `AvaloniaDialogService.cs` | Six dialogs, each built inside its public method: the alert (`ShowAlertCoreAsync`, 157 to 261), the alert of a `DialogMessage` (268 to 344), the confirmation of one (347 to 434), the input (829 to 902), the confirmation of a string (905 to 967) and the unsaved-changes prompt (985 to 1062). Each reads the desktop lifetime's main window (60 to 62), returns its no-window value when there is none, keeps the answer in a local its buttons set, and awaits `ShowDialog` on that window. The pickers open the platform's own dialogs, and `ShowSaveChangesDialogAsync` calls a factory the host registers |
| the same | Eleven buttons, the input's `TextBox`, a `SelectableTextBlock` for each block of a message, but a plain `TextBlock` for the confirmation of a string's message (934) and for the input's prompt (859), and in a `DialogMessage` a 📋 after each path and a ↗ after each link: `TextBlock`s with a hand cursor and a pointer-press handler, which a keyboard cannot reach, and the only controls with a tooltip (654 and 714). Nothing sets an automation name or id: a button is named by its label through its peer, and a glyph by its emoji. A window is named by its title, which begins with "⚠" or "⛔" for a destructive or an error dialog (239, 744 to 752) |
| the same | Literal colours: `#C62828` (217, 769 and 814), `#1F6FEB` (640, 700 and 711), `#888888` (651 and 770), `#C77700` (768) and `Brushes.White` (815); 768 to 770 choose a header's colour by its category, applied at 785, and 770 is unreachable, since the header returns before its switch for any category but Destructive and Error (761). A destructive button is filled with `#C62828` behind a white label (807 to 817). The code pane reads `AppCodeBlockBackgroundBrush` and `AppCodeBlockBorderBrush` (624 and 625), and is unstyled where a host declares neither |
| ClaudeForge, `App.axaml`, 15, 324 and 325, 410 and 411 | Takes Semi, through ScopedEditors' `SemiBundle.axaml`. Declares both keys, `#F4F4F4` and `#D0D0D0` in Light, `#1F1F1F` and `#3A3A3A` in Dark, and reads them in its own views; OpenForge2k's copy of ClaudeForge declares and reads them too. `docs/appservices.md` does not mention the keys |
| `Dialogs/FatalErrorDialog.cs`, `Dialogs/NonFatalNoticeDialog.cs` | Public window classes built in C#, each constructed with its content. Every interactive control has a name and help text, and its tooltip is the help text; the details box is a `TextBox`; none has an automation id. Neither sets a colour, and `FatalErrorDialog` is built to show before the application's resources have loaded (15 to 17) |
| `DialogAccessibilityTests` | Builds each of those two unshown and walks its logical tree: a name and a tooltip on every control its `IsInteractive` counts, among them a button, a text box, a `SelectableTextBlock` and a `TextBlock` with a hand cursor outside a button, with each count pinned. It reads `AutomationProperties.Name`, not the name the control's peer reports, and finds an emoji by a surrogate pair (105 and 112), which ⚠, ⛔ and ↗ are not. `plans/00005` left `AvaloniaDialogService`'s dialogs out, since no test could build them (its decision 6; `PROGRESS.md`, Next, by family) |
| `tests/AppServices.Tests` | `HeadlessTestApp` loads no theme, so a test's window paints no background of its own, and a button has no template. The project references no theme; `Directory.Packages.props` pins `Semi.Avalonia` 12.1.0 for ScopedEditors (33) |
| Avalonia 12.1.3, read in `reference/` | A text block's peer names it by its text and nothing else (`TextBlockAutomationPeer.cs`, 29), and an inline control puts U+FFFC in that text (`InlineUIContainer.cs`, 76 to 80); a window's peer names it by its title (`WindowAutomationPeer.cs`, 22). A resource dictionary takes one owner, and throws on a second (`ResourceProvider.cs`, 77 to 89). It looks in its own entries, then its theme dictionaries, then its merged dictionaries from the last to the first (`ResourceDictionary.cs`, 188 to 231), and the application looks in its resources, then its styles (`Application.cs`, 225 to 230). A dynamic resource is published again on each change of resources or of the control's theme (`DynamicResourceExpression.cs`, 85 to 94 and 131 to 143), and every top level takes the application's theme in effect (`TopLevel.cs`, 255 to 259 and 827 to 829). A window lists the windows it owns (`Window.cs`, 295). The Fluent theme's button names its presenter `PART_ContentPresenter`, and repaints it on pointer-over and on press (`Avalonia.Themes.Fluent/Controls/Button.xaml`, 31, 48 and 58) |
| Semi.Avalonia 12.1.0, read in `reference/` | Its default button names its presenter `PART_ContentPresenter` (`Controls/Button.axaml`, 115) and repaints its background and border on pointer-over and on press, from its own keys (163 to 171), whatever the button's own `Background`. Rendered on 2026-11-16 with those states forced, a destructive button's white label all but vanishes against the light fill in Light, and loses its red in Dark; and a flat button in a line of text, its fills cleared by a style on that part, looks as a text glyph does at rest, pointed at and pressed, in both themes, keyboard focus drawing the theme's ring |
| Inspections, `docs/ai-drivable-ui.md` | Every control needs a stable identity and an honest name: the id a harness finds it by as well as the name a screen reader reads (lines 46 to 50) |

## Decisions

| # | Decision | Why |
|---|---|---|
| 1 | **Each dialog is a window class of its own, built in C#, that a test constructs.** Six internal sealed classes in `Dialogs/`, beside `FatalErrorDialog`: `AlertDialog`, `MessageAlertDialog`, `ConfirmDialog`, `MessageConfirmDialog`, `InputDialog` and `UnsavedChangesDialog`. Each takes in its constructor what its method takes, with the service's `DialogAppIcon`, builds what the method builds today, and exposes the answer as `Result` once it closes: `bool?` for a confirmation, `string?` for the input, `UnsavedChangesChoice` for the prompt. The controls, the layout, the default and cancel buttons, the keys the input handles and the answers are moved, not changed, and the helpers that build a `DialogMessage`'s content and a category's header move with them | A window class is what makes `FatalErrorDialog` and `NonFatalNoticeDialog` testable: a test builds one, walks it, clicks it and reads its answer, with no owner and no modal loop. Internal, they add nothing to the public surface, and the solution's friend grants reach the tests |
| 2 | **`AvaloniaDialogService` keeps every public member, and shows what the classes build.** Each method asks for the owner, returns its no-window value when there is none, as today, constructs its dialog, awaits `ShowDialog` on the owner and returns `Result`. The owner comes from an internal `Func<Window?>`: the desktop lifetime's main window, unless a test sets another | The methods are left thin enough for a test to drive each end to end on a headless window it shows itself, finding the dialog among that window's owned windows |
| 3 | **Every window the package builds is a class a test constructs without an owner or a lifetime, and showing it is a separate step.** `src/AGENTS.md` holds the rule, and a sweep holds it: no method in the package's assembly constructs a plain `Window`, read from its IL, so every window is one of its classes, each in the test's table of windows (decision 4). `plans/00006`'s pattern takes the rule in for later controls | The six went untested because each was built where it was shown. A rule kept where contributors read it, and a sweep that fails the day a dialog is built inline again, stop the next one being built that way |
| 4 | **Every interactive control in every window the package builds has a name a screen reader reads, a tooltip that is not blank, and an automation id unique in its window**: the six, and `FatalErrorDialog` and `NonFatalNoticeDialog`, which gain ids, by the maintainer's choice of 2026-11-16. The name is the one the control's automation peer reports, which is what UI Automation reads. A button is named by its label, the input by its prompt, and the 📋 and ↗ buttons (decision 5) "Copy" and its path and "Open" and its link, through `AutomationProperties.Name`, which their peers read first. No name the package gives holds a symbol, a character of Unicode category `OtherSymbol` or one in the Arrows block. A text block is named by its text, as its peer names it. A button's tooltip says what a click does, and a text block's that its text can be selected and copied; where a control has help text, its tooltip is the same string, as in the two existing dialogs. Each id is `Dialog` and the control's role: `DialogOk`, `DialogConfirm`, `DialogCancel`, `DialogSave`, `DialogDontSave`, `DialogInput`, `DialogText`, `DialogCode`, `DialogCopyPath`, `DialogOpenLink`, `DialogDetails`, `DialogCopy` and `DialogClose`; a role that repeats in one window is numbered from its second, `DialogCopyPath2`. A window class the package declares that is missing from the test's table of windows, each with the arguments that build it, fails | A screen reader reads the peer's name, the person pointing reads the tooltip, and a harness finds the control by its id whatever label a caller passed. A text block's peer reads nothing but its text, so an automation name set on one changes nothing, and its text holds U+FFFC, itself a symbol, wherever an inline control sits: the symbol rule holds the names the package gives, not a block's text. The walker's surrogate check let ⚠, ⛔ and ↗ through. With ids in the two existing dialogs too, every window the package builds answers to one rule, and the table holds the next window the day it is added |
| 5 | **The 📋 after a path and the ↗ after a link are buttons that keep today's look, by the maintainer's choice of 2026-11-16.** Each is a `Button` in its text block's line, with no background, no border and no padding, no taller than the line, and with the dialog's own styles clearing the fill the theme paints on its `PART_ContentPresenter` on pointer-over and on press, as decision 6 does for a destructive button. A message looks as it does today in every state but keyboard focus, which the theme's focus ring shows. Tab reaches each, Enter and Space press it, and UI Automation invokes it; a press copies the path, or hands the link to the opener, as today's pointer press does. The link opener is an internal seam a test replaces. The 📋's glyph takes `AppServices.SubtleBrush`, and the ↗'s `AppServices.LinkBrush` | Today neither a keyboard user nor an agent can copy a path or open a link from a dialog. Rendered under Semi, the buttons look as today's glyphs at rest, pointed at and pressed, in both themes. The focus ring stays, since without it a keyboard user cannot see where focus is. As buttons, their peers read their names, so no control of the package's needs a peer of its own, and a test presses them without opening a browser |
| 6 | **Colours come from keys the package declares, in a `ResourceDictionary` it builds in C#, with a Light, a Dark and a Default theme dictionary, the Default holding the Light values, read through `DynamicResource`** (the table below). Light keeps today's colour, and Dark is chosen for a dark window, its contrast computed against stand-in windows, white for Light and `#1F1F1F` for Dark: 4.5:1 for text, 3:1 for a glyph. A destructive button keeps its filled red, by the maintainer's choice of 2026-11-16, and its dialog carries styles of its own for the button's pointer-over and pressed states, which paint the template's `PART_ContentPresenter` from the destructive keys, so its white label reads in every state. The code pane's keys take the package's prefix, by his choice the same day: `AppServices.CodeBlockBackgroundBrush` and `AppServices.CodeBlockBorderBrush` replace `AppCodeBlockBackgroundBrush` and `AppCodeBlockBorderBrush`, which are no longer read, and default to the values ClaudeForge declares for the old ones. The unreachable `#888888` at 770 goes | Every colour is declared once for each theme, under one prefix and a name a host can override, and an open dialog follows a change of theme. Light looks as it does today, and Dark reads as well as Light. The Default dictionary gives every key to a theme variant that inherits neither Light nor Dark. Semi and Fluent both repaint a button's `PART_ContentPresenter` on pointer-over and on press, so a style for that part, kept to the dialog's destructive buttons, wins there and nowhere else |
| 7 | **The package's dictionary reaches its dialogs with nothing for a host to do, and a host overrides any key**, by the maintainer's choice of 2026-11-16. The first window built in an application that reads a key merges a new instance of the dictionary into the application's resources, first among its merged dictionaries, unless one of its type is there already: the instance is never shared between applications, and its presence is read from the merged dictionaries, never from a flag. `FatalErrorDialog` and `NonFatalNoticeDialog` read no key, and merge nothing. A key a host declares in its application's resources, among its own entries or in a dictionary it merges, before or after, is found first | Avalonia reads merged dictionaries from the last to the first, so the first is read last, below everything the host declares. A dictionary takes one owner, so an instance shared between applications would throw in the second, as in the tests, which build an application for each. Merged when a dialog is built, it needs no call at start-up that a host could forget, which would leave the dialogs unstyled with no error. The two existing dialogs promise to show before the application's resources have loaded, so they never touch them |
| 8 | **Two sweeps over the package's IL hold the colours, with no baseline.** Nothing outside the dictionary's own type constructs a colour or a brush, or reads a member of `Colors`, `Brushes` or `KnownColors`: no constructor of `Color`, `SolidColorBrush` or `ImmutableSolidColorBrush`, and no `Color.Parse`, `Color.TryParse`, `Color.FromRgb`, `Color.FromArgb`, `Color.FromUInt32` or `Brush.Parse`. And each key is a constant of one internal class, looked up only through one internal helper, as ScopedEditors looks its keys up through `BrushHelper.Resolve`: no other method calls a resource lookup, which is `DynamicResourceExtension`'s constructor, `GetResourceObservable`, `FindResource`, `TryFindResource` or `TryGetResource`; every key is declared in the Light and the Dark dictionary; and every key is passed to the helper somewhere | Read from the IL, the sweeps see a colour however the source spells it. A guard written for one dialog holds that dialog alone; a sweep holds the next window the day it is added. `plans/00006` extends both to its controls and to ScopedEditors |
| 9 | **Every colour a window takes from the dictionary follows a change of theme while the window is open**, a destructive button's in each of its states. Tests show each window that takes one, set the application's `RequestedThemeVariant` from Light to Dark and back, and read each such colour in both | It is what `DynamicResource` promises, and what `plans/00006`'s switch relies on |
| 10 | **It ships in the AppServices release after `2026.4.1116`, with `plans/00008`, on the maintainer's word**, before `plans/00006`. It breaks one thing: a host that declared its own values under `AppCodeBlockBackgroundBrush` and `AppCodeBlockBorderBrush` declares them under the new keys. The change that stops reading the old keys is marked `!`, and `docs/appservices.md`'s "Upgrading from 2026.4.1116" says so, beside the dialogs' new colours in Dark and the 📋 and ↗ buttons. The day it is published, ClaudeForge's session is told, and declares the new keys at once | Releases are the maintainer's, and a breaking change is marked and written up where a consumer reads it. `plans/00008` changes the same eight windows, so one release brings both. ClaudeForge reworks as soon as the release is out, by the maintainer's word of 2026-11-17; until it does, its code panes look as they did, since the defaults equal its values |

### The keys

| Key | Replaces | Light | Dark | Measured as |
|---|---|---|---|---|
| `AppServices.ErrorBrush` | `#C62828` at 217 and 769, the error glyph | `#C62828`, 5.6:1 | `#F85149`, 4.9:1 | a glyph |
| `AppServices.WarningBrush` | `#C77700` at 768, the destructive glyph | `#C77700`, 3.5:1 | `#D29922`, 6.5:1 | a glyph |
| `AppServices.LinkBrush` | `#1F6FEB` at 640, 700 and 711: a path, a link and the ↗ | `#1F6FEB`, 4.6:1 | `#58A6FF`, 6.5:1 | text |
| `AppServices.SubtleBrush` | `#888888` at 651, the 📋 | `#888888`, 3.5:1 | `#8B949E`, 5.4:1 | a glyph |
| `AppServices.DestructiveBackgroundBrush` | `#C62828` at 814, a destructive button at rest | `#C62828` | `#C62828` | under its foreground |
| `AppServices.DestructivePointerOverBackgroundBrush` | the theme's fill on pointer-over | `#B71C1C` | `#B71C1C` | under its foreground |
| `AppServices.DestructivePressedBackgroundBrush` | the theme's fill on press | `#8E1B1B` | `#8E1B1B` | under its foreground |
| `AppServices.DestructiveForegroundBrush` | `Brushes.White` at 815 | `#FFFFFF`: 5.6:1, 6.6:1 and 9.0:1 on the three | the same | text |
| `AppServices.CodeBlockBackgroundBrush` | the host's `AppCodeBlockBackgroundBrush`, no longer read | `#F4F4F4` | `#1F1F1F` | not measured: the text on it is the theme's |
| `AppServices.CodeBlockBorderBrush` | the host's `AppCodeBlockBorderBrush`, no longer read | `#D0D0D0` | `#3A3A3A` | not measured |

### Dismissed

- **Public window classes.** A host shows them through `IDialogService`; public, each would be permanent
  API nobody asked for.
- **A view model behind each dialog.** The package has no markup and no view models, and a window class
  is what made the two existing dialogs testable.
- **Driving the dialogs through a real desktop lifetime**, as `DesktopProbe` runs one in a child
  process. It is slow, and its modal loop takes no click from a test.
- **Testing through the public methods alone.** Every check of a name or a colour would need a lifetime
  and a main window. The classes give the checks a window directly, and the owner seam keeps the methods
  covered.
- **Keeping `AppCodeBlockBackgroundBrush` and `AppCodeBlockBorderBrush` as the package's keys.** It would
  break nothing, but the maintainer chose one prefix for every key on 2026-11-16.
- **Reading the old keys as a fallback to the new ones.** Two keys for one colour, and a fallback that
  `DynamicResource` cannot express without code of its own.
- **The host merging the dictionary.** Every host would need a line at start-up, and one that forgot it
  would get unstyled dialogs with no error.
- **The dictionary in each dialog's own resources.** A key a host declares in its application would
  never be found, since a lookup reaches the window's resources first.
- **The colours of the host's theme**, Fluent's or Semi's. The package cannot know which theme a host
  runs, and each names its colours differently.
- **A red label on the theme's own button for a destructive action.** It reads in every state of any
  theme, but the maintainer kept the filled look on 2026-11-16, having seen both rendered.
- **Leaving a destructive button's pointer-over and pressed colours to the theme.** Under Semi in Light,
  its white label all but vanishes then.
- **An automation name on each text block**, such as "Message". Its peer ignores it, and a name that
  replaced the text would hide what the block shows.
- **The 📋 and ↗ as text with a peer of its own.** The smaller change, but a keyboard still could not
  reach them nor UI Automation press them; the maintainer chose buttons on 2026-11-16.

## Limits

- A dialog is named by its title, glyph and all: a screen reader reads "⚠" or "⛔" before the title of
  a destructive or an error dialog, which names its category. The maintainer kept the glyph on
  2026-11-16.
- Contrast is computed against stand-ins, white and `#1F1F1F`; a host's theme may paint a window
  otherwise.
- The dialog's styles, for a destructive button and for the 📋 and ↗, reach a theme whose button
  template names its presenter `PART_ContentPresenter`, as Semi's and Fluent's do; under any other,
  pointer-over and press keep the theme's colours and fills. The tests measure Semi; Fluent rests on its
  template's source.
- A glyph an emoji font draws in colour, such as 📋 or ⛔, may ignore its brush.
- Where focus rests on a 📋 or ↗ button, Enter presses it, not the dialog's default button.
- A key a host declares in a style's resources, under `Application.Styles`, is found after the package's
  dictionary, since the application reads its resources before its styles. A host declares an override
  in `Application.Resources`.
- A host that finds its own merged dictionaries by their index finds each one place later once a dialog
  has merged the package's.
- A dialog takes the application's theme, whatever its owner's: a window with a theme of its own shows
  its dialogs in the application's (`TopLevel.cs`, 255 to 259).
- A label, a path or a link a caller passes goes into a name as given, symbol and all; the tests hold
  the words the package gives.
- The pickers and a host's Save Changes dialog belong to the platform and the host.
- This plan's names and tooltips are English, as every string the package gives is today;
  `plans/00008`, built in the same work, lets a host replace them.

## Scope

**In:**
- decisions 1 to 10 and their tests;
- the doc comments;
- `src/AGENTS.md`: the rule for windows (decision 3), the row on names, tooltips and ids extended to
  every window, and the colour rules with their guards;
- the root `AGENTS.md`: AppServices' invariant on the two dialogs' tooltips, extended to every window,
  and to names and ids;
- `tests/AGENTS.md`: the `Dialogs/` row and its rules;
- `AppServices.Tests` referencing `Semi.Avalonia` at the version `Directory.Packages.props` already pins,
  to measure the destructive states and the 📋 and ↗ buttons under ClaudeForge's theme;
- `docs/appservices.md`: the keys, their defaults and how a host overrides one, and "Upgrading from
  2026.4.1116";
- `PROGRESS.md`;
- the Avalonia findings, sent to the XamlQuality session as the root `AGENTS.md` asks: that a text
  block's peer ignores its automation name and an inline control puts U+FFFC in its text, and what steps
  2 and 3 measure on the headless platform;
- a release of the AppServices family, with `plans/00008`;
- telling ClaudeForge's session, which takes the release at once.

**Out:**
- the pickers, and the factory behind `ShowSaveChangesDialogAsync`;
- the dialogs' strings, which `plans/00008` lets a host replace, in the same work and the same
  release;
- ClaudeForge's own keys and views;
- the theme switch, `plans/00006`, which follows this plan;
- ScopedEditors.

## Steps

| # | Step | Verified by |
|---|---|---|
| 1 | This plan, committed on its own once approved | CI green |
| 2 | The six window classes and the owner seam (decisions 1 and 2) | Headless tests, each seen red with the line it guards broken:<br>• each dialog's answer for each button, for Enter and Escape where they are wired, and for a close without a button (`null`, or `Cancel` for the prompt);<br>• the input's answer trimmed, and a blank one `null`;<br>• Enter is not the default on a destructive confirmation;<br>• each public method driven end to end through the owner seam, and its no-window value returned with no window built.<br>Measured first: that a headless window shows a dialog modally and lists it among its owned windows; where it cannot, the owner seam takes the show step as well, and `PROGRESS.md` says what the tests then do not reach. Package validation against `2026.4.1116` reports no breaking change, and the package's public types and members, listed by reflection before and after, are the same |
| 3 | Names, tooltips and ids, and the 📋 and ↗ buttons (decisions 4 and 5) | `DialogAccessibilityTests` walks all eight windows, those that take a `DialogMessage` with one holding a path, a link and a code block, and reads the name each control's automation peer reports: a name, with no symbol in one the package gives, a tooltip and an id unique in its window on every interactive control, and each count pinned. Under Semi, a 📋 or ↗ button is no taller than its line, and pointed at or pressed paints no fill. A click through the headless platform's mouse input on the glyph inside its text block, Enter, Space and UI Automation's invoke each make the 📋 put its path on the clipboard and the ↗ hand its link to the opener's seam, and the click selects no text. Each seen red with a name, a tooltip or an id removed, a symbol put in a name, an id repeated, a glyph made a text block again, the style that clears the fills removed, and a window class left out of the table. Measured first: that a button's and a text box's peers report their automation name; that an `InlineUIContainer`'s child is in its text block's logical tree, where the walker otherwise takes it as a root; and that Tab reaches a button in a text block's line, where its text block's tab navigation is otherwise set so that it does |
| 4 | The dictionary, the keys, the destructive states and the merge (decisions 6, 7 and 9) | Headless tests, each seen red with the line it guards broken:<br>• each key declared in the Light, Dark and Default dictionaries, with the values in the table;<br>• each colour's contrast against its stand-in, at its threshold, and the destructive foreground's on each of its three backgrounds;<br>• each window, shown, takes each colour from the dictionary, and follows a change of theme while open;<br>• under Semi, a destructive button pointed at and pressed is painted from its keys, its label still white;<br>• a key the host declares among its own resources, and in a dictionary it merges before the package's and after it, wins;<br>• building two dialogs merges one dictionary, first among the merged dictionaries; a second application gets an instance of its own; and `FatalErrorDialog` merges none |
| 5 | The sweeps (decisions 3 and 8), and `src/AGENTS.md`'s rules (decision 3) | Each sweep seen red with its defect planted: a plain `Window` constructed in the service, a `Color.Parse` outside the dictionary's type, a key declared in one dictionary only, a key passed to the helper nowhere, and a resource looked up outside the helper. Green with the six classes in place, and `LayeringTests` and the AssemblyQuality rules still pass |
| 6 | `docs/appservices.md`, the `AGENTS.md` rows, `PROGRESS.md` | `repo-conventions check` conforms. Package validation against `2026.4.1116` reports no breaking change; it cannot see a resource key, so the change that stops reading the two old keys is marked `!` by hand. CI green |
| 7 | Release the AppServices family at the day's version, with `plans/00008`, on the maintainer's word, through `docs/publishing.md` | Every id in the nuget.org flat container and restored from nuget.org alone, then `PROGRESS.md`'s Published row |
| 8 | The day it is published, tell ClaudeForge's session, which takes it at once: declare `AppServices.CodeBlockBackgroundBrush` and `AppServices.CodeBlockBorderBrush` where it declares the old keys. Send XamlQuality the Avalonia findings with their versions and sources | Told |
