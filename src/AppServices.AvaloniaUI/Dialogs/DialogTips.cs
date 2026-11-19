namespace Bennewitz.Ninja.AppServices.AvaloniaUI.Dialogs;

/// <summary>
/// The tooltip of each control in the dialogs <see cref="AvaloniaDialogService"/> shows
/// (<c>plans/00007</c>, decision 4): a button's says what a click does, and a block of text's that it can
/// be selected and copied.
/// </summary>
/// <remarks>
/// English, as every string the package gives is today. <c>plans/00008</c>, built in the same work, routes
/// each through a resolver a host replaces, under the key its table names.
/// </remarks>
internal static class DialogTips
{
    /// <summary>An alert's OK.</summary>
    internal const string Ok = "Close this message";

    /// <summary>The input's OK.</summary>
    internal const string Accept = "Use the text you typed";

    /// <summary>A confirmation's confirm button, whatever its label.</summary>
    internal const string Confirm = "Do what this dialog asks, and close it";

    /// <summary>A confirmation's cancel button, and the input's.</summary>
    internal const string Cancel = "Close this dialog without doing anything";

    /// <summary>The unsaved-changes prompt's Save.</summary>
    internal const string Save = "Save your changes, then close";

    /// <summary>The unsaved-changes prompt's Don't Save.</summary>
    internal const string DontSave = "Close without saving your changes";

    /// <summary>The unsaved-changes prompt's Cancel, which keeps the window open.</summary>
    internal const string KeepOpen = "Keep the window open, with your changes unsaved";

    /// <summary>The input's text box.</summary>
    internal const string Input = "Type here; Enter accepts it and Escape cancels";

    /// <summary>A block of a message's text, or an alert's message.</summary>
    internal const string SelectText = "Select text here to copy it";

    /// <summary>The text of a message's code pane.</summary>
    internal const string SelectCode = "Select code here to copy it";

    /// <summary>A 📋's tooltip once it has copied its path; its name stays <see cref="CopyPath"/>'s.</summary>
    internal const string Copied = "Copied!";

    /// <summary>A 📋's tooltip and name, which say what a press does.</summary>
    internal static string CopyPath(string path)
    {
        return $"Copy {path}";
    }

    /// <summary>A ↗'s tooltip and name, which say what a press does.</summary>
    internal static string OpenLink(string url)
    {
        return $"Open {url}";
    }
}
