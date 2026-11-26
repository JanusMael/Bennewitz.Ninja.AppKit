using System.Collections.Concurrent;
using System.Globalization;
using Serilog;
using Serilog.Events;

namespace Bennewitz.Ninja.AppServices.AvaloniaUI.Localization;

/// <summary>
/// Every string the package's windows show that no caller passes, each one a host replaces through
/// <see cref="Resolver"/>: labels, titles, tooltips, names and help text (<c>plans/00008</c>). What the host
/// does not replace reads in English.
/// </summary>
/// <remarks>
/// <para>
/// Each property is one string, and its name is its key. A host assigns <see cref="Resolver"/> once at
/// start-up, maps each key it translates to its own resources with <c>nameof</c>, and returns any other key
/// unchanged, as it does for ScopedEditors' <c>WrapperStrings</c>:
/// </para>
/// <code>
/// DialogStrings.Resolver = key => key switch
/// {
///     nameof(DialogStrings.LabelSave) => Strings.Save,
///     nameof(DialogStrings.LabelDontSave) => Strings.DontSave,
///     _ => key,
/// };
/// </code>
/// <para>
/// A string is read each time a window is built, and never kept: a culture the host changes reaches the next
/// window, and an open one keeps what it was built with.
/// </para>
/// <para>
/// Four strings carry a value as <c>{0}</c>: <see cref="TipCopyPath"/>, <see cref="TipOpenLink"/>,
/// <see cref="TitleFatalError"/> and <see cref="TitleNativeFatalError"/>. The package puts the value in with
/// the current culture, and a host's text without <c>{0}</c> shows without it.
/// </para>
/// <para>
/// A host's strings never fail a window. A resolver that throws, or returns null or blank text, or a text
/// whose <c>{0}</c> cannot be filled, gives that string its default, and the first such failure of each key
/// is logged at Warning. A key returned unchanged has not been translated: its default shows, and nothing is
/// logged.
/// </para>
/// <para>
/// The glyphs the package shows stay its own, outside the words: ⚠ and ⛔ before a title, the 📋 after a
/// path and before Copy to Clipboard, and the ↗ after a link. A label, a title or a message a caller passes
/// is shown as given.
/// </para>
/// </remarks>
public static class DialogStrings
{
    // The keys whose failure has been logged, so that each is logged once (plans/00008, decision 4).
    private static readonly ConcurrentDictionary<string, byte> _warned = new(StringComparer.Ordinal);

    /// <summary>
    /// Gives the text of a key, one of this class's property names. The default gives the package's
    /// English, and a host's returns any key it does not translate unchanged.
    /// </summary>
    /// <remarks>
    /// ⚠ It may be called on any thread, and must return at once: never wait on the UI thread in it. The
    /// native fatal-error dialog's title is read on whichever thread failed, so a resolver that waits there
    /// can hang the application's last report.
    /// </remarks>
    public static Func<string, string> Resolver { get; set; } = DefaultResolver;

    /// <summary>An alert's OK button, and the input dialog's: "OK".</summary>
    public static string LabelOk => Text(nameof(LabelOk));

    /// <summary>
    /// The cancel button of the input dialog and of the unsaved-changes prompt, and a confirmation's when its
    /// caller passes no label: "Cancel".
    /// </summary>
    public static string LabelCancel => Text(nameof(LabelCancel));

    /// <summary>A confirmation's confirm button when its caller passes no label: "Confirm".</summary>
    public static string LabelConfirm => Text(nameof(LabelConfirm));

    /// <summary>The unsaved-changes prompt's Save button: "Save".</summary>
    public static string LabelSave => Text(nameof(LabelSave));

    /// <summary>The unsaved-changes prompt's Don't Save button: "Don't Save".</summary>
    public static string LabelDontSave => Text(nameof(LabelDontSave));

    /// <summary>The folder picker's title when its caller passes none: "Select Folder".</summary>
    public static string TitleSelectFolder => Text(nameof(TitleSelectFolder));

    /// <summary>The file picker's title when its caller passes none: "Select File".</summary>
    public static string TitleSelectFile => Text(nameof(TitleSelectFile));

    /// <summary>The save picker's title when its caller passes none: "Save As".</summary>
    public static string TitleSaveAs => Text(nameof(TitleSaveAs));

    /// <summary>The tooltip and the name of the 📋 after a path, the path as <c>{0}</c>: "Copy {0}".</summary>
    public static string TipCopyPath => Text(nameof(TipCopyPath));

    /// <summary>
    /// The tooltip of a 📋 once it has copied its path, whose name stays <see cref="TipCopyPath"/>'s:
    /// "Copied!".
    /// </summary>
    public static string TipCopied => Text(nameof(TipCopied));

    /// <summary>The tooltip and the name of the ↗ after a link, the link as <c>{0}</c>: "Open {0}".</summary>
    public static string TipOpenLink => Text(nameof(TipOpenLink));

    /// <summary>The label and the name of the fatal-error and notice dialogs' close button: "Close".</summary>
    public static string LabelClose => Text(nameof(LabelClose));

    /// <summary>The tooltip of the fatal-error and notice dialogs' close button: "Close this window".</summary>
    public static string TipCloseWindow => Text(nameof(TipCloseWindow));

    /// <summary>
    /// The name of the fatal-error and notice dialogs' copy button, and its label after the package's 📋:
    /// "Copy to Clipboard".
    /// </summary>
    public static string LabelCopyToClipboard => Text(nameof(LabelCopyToClipboard));

    /// <summary>The name of the fatal-error dialog's details box: "Error details".</summary>
    public static string NameErrorDetails => Text(nameof(NameErrorDetails));

    /// <summary>
    /// The help text and the tooltip of the fatal-error dialog's details box: "Read-only exception text;
    /// Copy to Clipboard copies all of it".
    /// </summary>
    public static string HelpErrorDetails => Text(nameof(HelpErrorDetails));

    /// <summary>
    /// The help text and the tooltip of the fatal-error dialog's copy button: "Copy the full error details to
    /// the clipboard".
    /// </summary>
    public static string HelpCopyErrorDetails => Text(nameof(HelpCopyErrorDetails));

    /// <summary>The name of the notice dialog's details box: "Notice details".</summary>
    public static string NameNoticeDetails => Text(nameof(NameNoticeDetails));

    /// <summary>
    /// The help text and the tooltip of the notice dialog's details box: "Read-only notice text; Copy to
    /// Clipboard copies all of it".
    /// </summary>
    public static string HelpNoticeDetails => Text(nameof(HelpNoticeDetails));

    /// <summary>
    /// The help text and the tooltip of the notice dialog's copy button: "Copy the full notice text to the
    /// clipboard".
    /// </summary>
    public static string HelpCopyNotice => Text(nameof(HelpCopyNotice));

    /// <summary>
    /// The message of the fatal-error dialog the package shows, and of one whose caller passes none: "The
    /// application encountered an unexpected error.", then on a line of its own "You can copy the details
    /// below for bug reporting.".
    /// </summary>
    public static string TextFatalError => Text(nameof(TextFatalError));

    /// <summary>
    /// The fatal-error dialog's title, the application's name as <c>{0}</c>: "Application Error — {0}".
    /// </summary>
    public static string TitleFatalError => Text(nameof(TitleFatalError));

    /// <summary>
    /// The title of the fatal-error dialog, and of the native one, before
    /// <see cref="AvaloniaDiagnostics.ConfigureLogging"/> has named the application: "Application Error".
    /// </summary>
    public static string TitleFatalErrorNoApp => Text(nameof(TitleFatalErrorNoApp));

    /// <summary>
    /// The native fatal-error dialog's title, the application's name as <c>{0}</c>: "{0} — Critical Error".
    /// </summary>
    public static string TitleNativeFatalError => Text(nameof(TitleNativeFatalError));

    /// <summary>The tooltip of an alert's OK button: "Close this message".</summary>
    public static string TipOk => Text(nameof(TipOk));

    /// <summary>The tooltip of the input dialog's OK button: "Use the text you typed".</summary>
    public static string TipAccept => Text(nameof(TipAccept));

    /// <summary>
    /// The tooltip of a confirmation's confirm button, whatever its label: "Do what this dialog asks, and
    /// close it".
    /// </summary>
    public static string TipConfirm => Text(nameof(TipConfirm));

    /// <summary>
    /// The tooltip of a confirmation's cancel button, and of the input dialog's: "Close this dialog without
    /// doing anything".
    /// </summary>
    public static string TipCancel => Text(nameof(TipCancel));

    /// <summary>The tooltip of the unsaved-changes prompt's Save button: "Save your changes, then close".</summary>
    public static string TipSave => Text(nameof(TipSave));

    /// <summary>
    /// The tooltip of the unsaved-changes prompt's Don't Save button: "Close without saving your changes".
    /// </summary>
    public static string TipDontSave => Text(nameof(TipDontSave));

    /// <summary>
    /// The tooltip of the unsaved-changes prompt's cancel button, labelled <see cref="LabelCancel"/>: "Keep
    /// the window open, with your changes unsaved".
    /// </summary>
    public static string TipKeepOpen => Text(nameof(TipKeepOpen));

    /// <summary>
    /// The tooltip of the input dialog's text box: "Type here; Enter accepts it and Escape cancels".
    /// </summary>
    public static string TipInput => Text(nameof(TipInput));

    /// <summary>The tooltip of a message's text: "Select text here to copy it".</summary>
    public static string TipSelectText => Text(nameof(TipSelectText));

    /// <summary>The tooltip of a message's code: "Select code here to copy it".</summary>
    public static string TipSelectCode => Text(nameof(TipSelectCode));

    /// <summary><see cref="TipCopyPath"/>, with <paramref name="path"/> put in.</summary>
    internal static string TipCopyPathFor(string path) => Filled(nameof(TipCopyPath), path);

    /// <summary><see cref="TipOpenLink"/>, with <paramref name="url"/> put in.</summary>
    internal static string TipOpenLinkFor(string url) => Filled(nameof(TipOpenLink), url);

    /// <summary><see cref="TitleFatalError"/>, with <paramref name="appName"/> put in.</summary>
    internal static string TitleFatalErrorFor(string appName) => Filled(nameof(TitleFatalError), appName);

    /// <summary><see cref="TitleNativeFatalError"/>, with <paramref name="appName"/> put in.</summary>
    internal static string TitleNativeFatalErrorFor(string appName) => Filled(nameof(TitleNativeFatalError), appName);

    /// <summary>Puts the default <see cref="Resolver"/> back and forgets which keys have failed. Test-only.</summary>
    internal static void ResetForTesting()
    {
        Resolver = DefaultResolver;
        _warned.Clear();
    }

    /// <summary>
    /// The host's text for <paramref name="key"/>, or its default when the resolver throws, gives null or
    /// blank text, or gives the key back.
    /// </summary>
    private static string Text(string key)
    {
        string fallback = DefaultResolver(key);
        string? text;
        try
        {
            text = Resolver(key);
        }
        catch (Exception exception)
        {
            Warn(key, "threw", exception);
            return fallback;
        }

        if (text is null)
        {
            Warn(key, "gaveNull", null);
            return fallback;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            Warn(key, "gaveBlank", null);
            return fallback;
        }

        // A key given back has not been translated: the host maps only what it translates.
        return string.Equals(text, key, StringComparison.Ordinal) ? fallback : text;
    }

    /// <summary>
    /// The text of <paramref name="key"/> with <paramref name="value"/> put in as its <c>{0}</c>, or its
    /// default's when the host's text cannot take it.
    /// </summary>
    private static string Filled(string key, string value)
    {
        string text = Text(key);
        try
        {
            return string.Format(CultureInfo.CurrentCulture, text, value);
        }
        catch (FormatException exception)
        {
            Warn(key, "badPlaceholder", exception);
            return string.Format(CultureInfo.CurrentCulture, DefaultResolver(key), value);
        }
    }

    /// <summary>
    /// Logs the first failure of <paramref name="key"/> at Warning, and nothing for the next: the resolver
    /// gave no text the key can show, so its default is shown (<c>plans/00009</c>, decision 7).
    /// </summary>
    /// <param name="key">The key whose text failed.</param>
    /// <param name="reason">How it failed: <c>threw</c>, <c>gaveNull</c>, <c>gaveBlank</c> or <c>badPlaceholder</c>.</param>
    /// <param name="exception">What the resolver, or the text's format, threw, if anything.</param>
    private static void Warn(string key, string reason, Exception? exception)
    {
        if (!_warned.TryAdd(key, 0))
        {
            return;
        }

        try
        {
            Log.Write(LogEventLevel.Warning, exception, "[Dialog.Strings] key={Key} reason={Reason}", key, reason);
        }
        catch (Exception)
        {
            // ⛔ A logger that throws must not cost the user the window, the fatal-error dialog above all.
        }
    }

    private static string DefaultResolver(string key)
    {
        return key switch
        {
            nameof(LabelOk) => "OK",
            nameof(LabelCancel) => "Cancel",
            nameof(LabelConfirm) => "Confirm",
            nameof(LabelSave) => "Save",
            nameof(LabelDontSave) => "Don't Save",
            nameof(TitleSelectFolder) => "Select Folder",
            nameof(TitleSelectFile) => "Select File",
            nameof(TitleSaveAs) => "Save As",
            nameof(TipCopyPath) => "Copy {0}",
            nameof(TipCopied) => "Copied!",
            nameof(TipOpenLink) => "Open {0}",
            nameof(LabelClose) => "Close",
            nameof(TipCloseWindow) => "Close this window",
            nameof(LabelCopyToClipboard) => "Copy to Clipboard",
            nameof(NameErrorDetails) => "Error details",
            nameof(HelpErrorDetails) => "Read-only exception text; Copy to Clipboard copies all of it",
            nameof(HelpCopyErrorDetails) => "Copy the full error details to the clipboard",
            nameof(NameNoticeDetails) => "Notice details",
            nameof(HelpNoticeDetails) => "Read-only notice text; Copy to Clipboard copies all of it",
            nameof(HelpCopyNotice) => "Copy the full notice text to the clipboard",
            nameof(TextFatalError) =>
                "The application encountered an unexpected error.\nYou can copy the details below for bug reporting.",
            nameof(TitleFatalError) => "Application Error — {0}",
            nameof(TitleFatalErrorNoApp) => "Application Error",
            nameof(TitleNativeFatalError) => "{0} — Critical Error",
            nameof(TipOk) => "Close this message",
            nameof(TipAccept) => "Use the text you typed",
            nameof(TipConfirm) => "Do what this dialog asks, and close it",
            nameof(TipCancel) => "Close this dialog without doing anything",
            nameof(TipSave) => "Save your changes, then close",
            nameof(TipDontSave) => "Close without saving your changes",
            nameof(TipKeepOpen) => "Keep the window open, with your changes unsaved",
            nameof(TipInput) => "Type here; Enter accepts it and Escape cancels",
            nameof(TipSelectText) => "Select text here to copy it",
            nameof(TipSelectCode) => "Select code here to copy it",
            var _ => key,
        };
    }
}
