# 00008 — Dialog strings a host can replace

> Status: **approved 2026-11-17**. Supersedes nothing. The maintainer's choice of 2026-11-17, made
> while reading `plans/00007`'s scope: the dialogs' strings get a plan of their own, built in the same
> work as `plans/00007` and shipped in the same release.

Every string the package's windows show that no caller passes is an English literal, and no host can
replace one. ClaudeForge localises most of its interface through its own resources, yet its dialogs
still show the package's English: its alerts say "OK", its unsaved-changes prompt "Save", "Don't Save"
and "Cancel", its file pickers "Select Folder", and its crash dialog "Close" and "Copy to Clipboard".
`plans/00007` adds a tooltip to every control of the package's eight windows, and a name to each 📋
and ↗ button: each another such string. This plan routes every one through a resolver the host sets
once, as ScopedEditors' `WrapperStrings` does for that family's chrome.

## The requirement

| # | From the request |
|---|---|
| a | Every string the package shows that no caller passes can be replaced by the host: labels, titles, tooltips, names and help text alike. The maintainer's choice of 2026-11-17 |
| b | A string the host does not replace reads as it does today, in English |
| c | A host's strings that throw or come back unusable never stop a dialog from showing, the fatal-error dialog above all |
| d | Built in the same work as `plans/00007` and released with it, as the maintainer expected ("probably") |

## What exists

Read on 2026-11-17.

| Source | What it shows |
|---|---|
| `AvaloniaDialogService.cs` | Literal labels: "OK" (168, 279 and 846), "Cancel" (847 and 1007), "Save" (1001) and "Don't Save" (1004). The pickers' titles when a caller passes none: "Select Folder" (79), "Select File" (98) and "Save As" (124). The 📋's tooltip `$"Copy {path}"` (654), "Copied!" once it has copied (668), and the ↗'s `$"Open {url}"` (714) |
| `IDialogService.cs` | Both `ShowConfirmAsync` overloads default `confirmLabel` to "Confirm" and `cancelLabel` to "Cancel" (83 to 85, 102 to 107). A default is compiled into each call that leaves its argument out, so the service receives the English word |
| `Dialogs/FatalErrorDialog.cs` | "📋 Copy to Clipboard" (112), named "Copy to Clipboard" (118); "Close" (125 and 128) with the tooltip "Close this window" (129); the details box named "Error details" (98); two help texts, each also a tooltip (97 and 117); and `DefaultMessage`, a public constant (40 to 42), which a constant's callers compile into themselves; the package itself passes it at `AvaloniaDiagnostics.cs` 350 and `UiThreadExceptions.cs` 222 |
| `Dialogs/NonFatalNoticeDialog.cs` | The same three controls' strings, for a notice: 79, 80, 94, 99, 100, 107, 110 and 111 |
| `AvaloniaDiagnostics.cs` | The fatal dialog's title, "Application Error — {AppName}", or "Application Error" before `ConfigureLogging` (108 to 112); the native fatal dialog's, "{AppName} — Critical Error" or "Application Error" (375 to 380), read on whichever thread failed |
| `AvaloniaDiagnosticsOptions.cs`, `UiThreadExceptions.cs` | The package already guards a host's code and its own logger: `IsBenignException` "must never wait on the UI thread" (290), and every line the UI-thread handler logs goes through a write that swallows a logger's throw (438 to 448), which `UiThreadExceptionHandlerTests` plants with a throwing logger |
| `Dialogs/NativeErrorDialog.cs`, in `AppServices` | Shows the title and message it is given. Its buttons are the platform's, but for macOS's `osascript`, whose button the package names "OK" (129). The package cannot see the Avalonia package |
| ScopedEditors, `Localization/WrapperStrings.cs` | A public static class: a `Func<string, string>` `Resolver` (53), a public string property for each string, keyed by the property's name, an English default for each (55 to 78), defaults that name no product (`WrapperStringsNeutralityTests`), and `ResetForTesting` for the tests (154 to 157). Read through `{x:Static}`, so a host must set it before the markup loads |
| ClaudeForge, `Program.cs` | Applies its culture (120), then assigns `WrapperStrings.Resolver` a switch from 13 of its 17 keys to its own `Strings` resources, returning the key for the other four (133 to 154) |
| ClaudeForge, its calls | Passes both labels in some confirmations (`AboutEditorViewModel.cs`, 434 to 439) and only `confirmLabel` in most (`BackupRestoreViewModel.cs`, 553 to 555), but passes English of its own too: titles, prompts, placeholders and labels in `ProfilesViewModel.cs` (160 to 813), "Delete" and "Create junction" in `BackupRestoreViewModel.cs` (1042 and 1206), and "Save anyway" and "Cancel" in `MainWindowViewModel.cs` (1981 and 1982). Its alerts, its input dialog and its unsaved-changes prompt (`MainWindow.axaml.cs`, 387) take no label. Its tests hold 31 classes that implement `IDialogService` |

## Decisions

| # | Decision | Why |
|---|---|---|
| 1 | **One resolver, `DialogStrings.Resolver`, in `AppServices.Avalonia`, shaped as ScopedEditors' `WrapperStrings`**, the package by the maintainer's choice of 2026-11-17. `DialogStrings` is a public static class in `Localization/`: a `Func<string, string>` `Resolver`, a public static string property for each string, keyed by the property's name, a default resolver returning today's English, and an internal `ResetForTesting`. A host assigns `Resolver` at start-up, mapping each key it translates to its own resources with `nameof`, as ClaudeForge maps `WrapperStrings`'. Its doc says it may be called on any thread and must return at once, never waiting on the UI thread, as `IsBenignException`'s does | One pattern for both families, which a host already wires once. The package builds its windows on demand, where a host never holds one, and the fatal dialog opens from a handler no service instance reaches: a static resolver reaches both. `nameof` makes a host's keys compile-checked. The native fatal dialog's title is read on whichever thread failed, so a resolver that waited there could hang the app's last report |
| 2 | **A string is read when its window is built, never cached.** A culture the host changes reaches the next dialog; an open one keeps what it was built with | The windows are built in C# when shown, so nothing here needs `{x:Static}`'s parse-time read, nor its rule that the resolver be set before anything loads |
| 3 | **It covers every string the package gives but one**: in the eight windows, every label, tooltip, automation name and help text, the pickers' default titles, the fatal dialogs' titles and default message, and the two default confirmation labels (decision 6), with the names and tooltips `plans/00007` adds. The one left out is the button macOS's native fatal dialog names "OK" (`NativeErrorDialog.cs`, 129), in `AppServices`, which cannot see the Avalonia package. Not text a caller passes, and not what the package writes to a log, an exception or stderr, which stays English for whoever reads a log | Those are what a person using the app reads or hears. A log is read by whoever diagnoses it, in the language of the code that wrote it |
| 4 | **A host's strings that throw or come back unusable never fail a dialog.** A resolver that throws, or returns null or blank text, gives that string's default, as does a template whose placeholder cannot be filled. The first such failure of each key is logged at Warning, through a write that swallows a logger's own throw; a set safe across threads, which `ResetForTesting` clears, keeps it to one. A resolver that returns the key itself has not translated it: the default shows, and nothing is logged | The fatal dialog shows after something has failed, perhaps before the host's own resources work, and the logger may be what failed. Returning the key is how `WrapperStrings`' resolvers decline a key, ClaudeForge's for four of its seventeen: a host maps only what it translates, and a Warning for each key it leaves would fill every run's log |
| 5 | **A string that carries a value takes it as `{0}`**, formatted with the current culture: `TipCopyPath`, "Copy {0}"; `TipOpenLink`, "Open {0}"; `TitleFatalError`, "Application Error — {0}"; `TitleNativeFatalError`, "{0} — Critical Error". The 📋's and the ↗'s automation names are these tooltips' text, as `plans/00007` names them by "Copy" and its path and "Open" and its link, and the 📋's stays so after a copy, when its tooltip reads `TipCopied` | A translation puts the value where its language needs it. One string for a control's tooltip and its name keeps what is seen and what is heard the same; after a copy, the name still says what a press does |
| 6 | **The two confirmation labels default to null in `IDialogService`, and null shows the host's words**, by the maintainer's choice of 2026-11-17. Both `ShowConfirmAsync` overloads take `string? confirmLabel = null` and `string? cancelLabel = null`, and their docs say so; the service shows `DialogStrings.LabelConfirm` or `DialogStrings.LabelCancel` for null, and any label given as given. Every class that implements the contract changes those two parameters to match, ClaudeForge's 31 test doubles among them: a breaking change, marked `!`, and written in `docs/appservices.md`'s "Upgrading from 2026.4.1116" beside `plans/00007`'s | The contract says what it means: no label, the host's word. An English default compiled into each caller could only reach the host's words by treating "Confirm" and "Cancel" specially forever. ClaudeForge's session changes its doubles at once, by the maintainer's word of 2026-11-17 |
| 7 | **The defaults name no product**, held by a test, as `WrapperStringsNeutralityTests` holds ScopedEditors'. The 📋 on the copy buttons stays the package's, set before the host's words | A host that wires no resolver ships the defaults as they are. The glyph is a symbol, which no name may hold (`plans/00007`, decision 4), so it is kept apart from the words |
| 8 | **`FatalErrorDialog.DefaultMessage` goes, and `DialogStrings.TextFatalError` replaces it**, by the maintainer's choice of 2026-11-17. The package's own dialogs show `TextFatalError`, whose default is the constant's text, and a host that passed the constant passes `TextFatalError` or a message of its own: a breaking change, marked `!` beside decision 6's | Kept, the constant would stay English beside a string a host translates, and a host that passed it would show English. The maintainer wants every breaking change in this one release, and no host of his uses it: ClaudeForge, Templates and DiffView, read on 2026-11-17 |
| 9 | **Tests hold it.** A resolver that returns a marker for every key builds every window in `plans/00007`'s table of windows, with a `DialogMessage` holding a path, a link and a code block, and presses each 📋; every label, tooltip, name, help text and title the package gives in a window then reads as a marker, so a string left English there fails. The strings outside a window, the pickers' default titles, the fatal dialogs' titles and the two default confirmation labels, are each held by a test of their own, which `src/AGENTS.md` asks of the next such string. Every key has a property and a default | The walk finds a string added to a window the day it is added, wherever in the window it is written. What it cannot reach is named, and each has its own test |
| 10 | **It ships with `plans/00007`, in one AppServices release, on the maintainer's word.** Should this plan slip, `plans/00007` waits for it: the maintainer wants every breaking change in one release, by his word of 2026-11-17. The day it is published, ClaudeForge's session is told, and at once assigns `DialogStrings.Resolver` from its own resources, beside its `WrapperStrings` line, changes its test doubles' two parameters, and replaces the English its own calls pass | Both plans change the same eight windows; one release puts them before a host at once. ClaudeForge reworks as soon as the release is out, by the maintainer's word of 2026-11-17; its own English would otherwise stay English beside the package's translated words |

### The strings

The ones the windows show today, then those `plans/00007` adds, which its step 3 writes with these
defaults.

| Key | Default | Where |
|---|---|---|
| `LabelOk` | OK | `AvaloniaDialogService.cs` 168, 279, 846 |
| `LabelCancel` | Cancel | 847, 1007; and an omitted `cancelLabel` |
| `LabelConfirm` | Confirm | an omitted `confirmLabel` |
| `LabelSave` | Save | 1001 |
| `LabelDontSave` | Don't Save | 1004 |
| `TitleSelectFolder` | Select Folder | 79 |
| `TitleSelectFile` | Select File | 98 |
| `TitleSaveAs` | Save As | 124 |
| `TipCopyPath` | Copy {0} | 654 |
| `TipCopied` | Copied! | 668 |
| `TipOpenLink` | Open {0} | 714 |
| `LabelClose` | Close | `FatalErrorDialog.cs` 125, 128; `NonFatalNoticeDialog.cs` 107, 110 |
| `TipCloseWindow` | Close this window | 129; 111 |
| `LabelCopyToClipboard` | Copy to Clipboard | 112, 118; 94, 100 |
| `NameErrorDetails` | Error details | 98 |
| `HelpErrorDetails` | Read-only exception text; Copy to Clipboard copies all of it | 97 |
| `HelpCopyErrorDetails` | Copy the full error details to the clipboard | 117 |
| `NameNoticeDetails` | Notice details | 80 |
| `HelpNoticeDetails` | Read-only notice text; Copy to Clipboard copies all of it | 79 |
| `HelpCopyNotice` | Copy the full notice text to the clipboard | 99 |
| `TextFatalError` | The application encountered an unexpected error.`\n`You can copy the details below for bug reporting. | 40 to 42, the constant it replaces |
| `TitleFatalError` | Application Error — {0} | `AvaloniaDiagnostics.cs` 112 |
| `TitleFatalErrorNoApp` | Application Error | 112, 378 |
| `TitleNativeFatalError` | {0} — Critical Error | 379 |
| `TipOk` | Close this message | `plans/00007`: the alerts' OK |
| `TipAccept` | Use the text you typed | the input's OK |
| `TipConfirm` | Do what this dialog asks, and close it | a confirmation's confirm button, whatever its label |
| `TipCancel` | Close this dialog without doing anything | a confirmation's Cancel and the input's |
| `TipSave` | Save your changes, then close | the unsaved-changes prompt's Save |
| `TipDontSave` | Close without saving your changes | its Don't Save |
| `TipKeepOpen` | Keep the window open, with your changes unsaved | its Cancel, labelled `LabelCancel` |
| `TipInput` | Type here; Enter accepts it and Escape cancels | the input's text box |
| `TipSelectText` | Select text here to copy it | a message's text |
| `TipSelectCode` | Select code here to copy it | a message's code |

### Dismissed

- **Translations shipped in the package**, as resources with satellite assemblies. Every host would keep
  a second set of translations in step with its own, the reason `WrapperStrings` gives for its resolver,
  and the host already has the words.
- **A strings record set on the service.** The fatal dialogs open where no service instance reaches, so a
  host would set it in two places, and build it again at each change of culture.
- **Strings as properties of each dialog**, as `plans/00006`'s controls take theirs. A host never holds a
  dialog; the service builds it.
- **Strings as resource keys, beside `plans/00007`'s colours.** The fatal dialog is built to show before
  the application's resources have loaded (`FatalErrorDialog.cs`, 15 to 17), and the native dialog's
  title is read on whichever thread failed, where no resource can be read.
- **`IStringLocalizer`.** A dependency the Avalonia package's tier forbids (`LayeringTests.Tiers`). A host
  that keeps one adapts it in a line, `key => localizer[key].Value`, whose value for a key it lacks is
  the key itself.
- **`DialogStrings` in `AppServices.Abstractions`**, where `NativeErrorDialog` could read it too. The
  contracts package would carry process-wide state into every test and non-UI host that takes it for its
  fakes, for macOS's one "OK"; the maintainer kept it in the Avalonia package on 2026-11-17.
- **Reusing `WrapperStrings`.** It belongs to ScopedEditors, which nothing here may reference.
- **A sweep of the IL's string literals**, as `plans/00007` sweeps its colours. It would need a list of
  the literals allowed, for log text, automation ids, resource keys and font names: a list kept by hand
  in another form.
- **Swapping the English words "Confirm" and "Cancel" for the host's, with the contract unchanged.**
  Nothing would break, but the two words would be special forever, a caller's deliberate English among
  them; the maintainer chose the null defaults on 2026-11-17.
- **Null defaults and the swap together.** Implementers break, and the quirk stays.
- **Keeping `FatalErrorDialog.DefaultMessage`.** It breaks nothing, but stays English beside a string a
  host translates; the maintainer removed it on 2026-11-17, with every breaking change in one release.
- **Localising the log, exception and stderr text.** Its reader diagnoses the code, not the app.

## Limits

- An open dialog keeps the strings it was built with; a change of culture shows at the next one.
- A call compiled against the old contract sends "Confirm" and "Cancel" as labels, shown in English
  until it is rebuilt; and a caller that passes the English word itself gets it, as ClaudeForge's
  `MainWindowViewModel.cs` does at 1982.
- The resolver runs synchronously, on any thread: the native fatal dialog's title is read on whichever
  thread failed. One that blocks holds the dialog with it.
- A host's word goes into a name as given, symbol and all; the tests hold the package's defaults.
- A host's template without `{0}` drops the value: two 📋 in one window then share a name, though their
  ids differ.
- Every key is public, and permanent once released: a string the package stops showing keeps its key,
  and no key is renamed.
- Layout stays left to right whatever the language.

## Scope

**In:**
- decisions 1 to 10 and their tests;
- `IDialogService`'s two parameters in `AppServices.Abstractions`, and their docs (decision 6);
- the doc comments, `Resolver`'s warning among them;
- `src/AGENTS.md`: a row that every string the package gives goes through `DialogStrings`, held by the
  marker walk in a window and by a test of its own outside one;
- the root `AGENTS.md`: AppServices' invariant on the windows' strings;
- `tests/AGENTS.md`: `DialogStrings` among the static state a test puts back in `Dispose`;
- `docs/appservices.md`: the keys, their defaults and a host's resolver, beside `plans/00007`'s
  "Upgrading from 2026.4.1116";
- `PROGRESS.md`;
- the release, with `plans/00007`;
- telling ClaudeForge's session, with the calls where it passes English itself.

**Out:**
- translations, which the host supplies;
- text a caller passes, and ClaudeForge's own calls, which its session changes;
- log, exception and stderr text;
- the platform's own dialogs and their buttons, macOS's native "OK" among them, and a host's Save
  Changes dialog;
- `plans/00006`'s strings, properties of its controls;
- ScopedEditors and `WrapperStrings`;
- right-to-left layout.

## Steps

| # | Step | Verified by |
|---|---|---|
| 1 | This plan, committed on its own once approved | CI green |
| 2 | `DialogStrings`, and every string decision 3 names routed through it, on top of `plans/00007`'s steps 2 to 5 | Headless tests, each seen red with the line it guards broken, and each one that sets the resolver putting the default back in `Dispose`, as `tests/AGENTS.md` asks of static state:<br>• the marker walk over every window in the table, each 📋 pressed, every key marked, and a literal planted in one window failing it;<br>• the pickers' titles, the fatal dialogs' titles and the two default confirmation labels, each marked by a test of its own, a null label showing the marker and a label given shown as given;<br>• each failure of decision 4 giving its default, one Warning for each failing key, none for a key returned unchanged, and the fatal dialog still built when the logger throws as well;<br>• every key with a property and a default;<br>• no default naming a product.<br>Every existing test still passes unchanged, since each default is today's text. Measured first: whether a test reads the options a picker is opened with from the headless platform, or through an internal step that builds them. Package validation against `2026.4.1116` reports nothing breaking beyond the removed `FatalErrorDialog.DefaultMessage` (`CP0002`) and `IDialogService`'s two parameters, and the changes that make them are marked `!` whatever it reports |
| 3 | `docs/appservices.md`, the `AGENTS.md` rows and `PROGRESS.md`, with `plans/00007`'s step 6 | `repo-conventions check` conforms. CI green |
| 4 | Release, as `plans/00007`'s step 7 | As that step |
| 5 | Tell ClaudeForge's session the day it is published, with `plans/00007`'s step 8: assign `DialogStrings.Resolver` from its resources for every key in the table, change its test doubles' two parameters, and replace the English its own calls pass | Told |
