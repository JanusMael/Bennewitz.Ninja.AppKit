using System.Globalization;

namespace Bennewitz.Ninja.AppServices.AvaloniaUI.Dialogs;

/// <summary>
/// The automation id of each interactive control in the windows the package builds: <c>Dialog</c> and the
/// control's role (<c>plans/00007</c>, decision 4). A harness finds a control by its id, whatever label a
/// caller passed.
/// </summary>
/// <remarks>
/// A role a window repeats is numbered from its second control, <c>DialogCopyPath2</c>, by one
/// <see cref="Sequence"/> for the window's message.
/// </remarks>
internal static class DialogAutomationIds
{
    /// <summary>The button that closes an alert, or accepts the input.</summary>
    internal const string Ok = "DialogOk";

    /// <summary>A confirmation's confirm button, whatever its label.</summary>
    internal const string Confirm = "DialogConfirm";

    /// <summary>The button that closes a dialog without its action.</summary>
    internal const string Cancel = "DialogCancel";

    /// <summary>The unsaved-changes prompt's Save.</summary>
    internal const string Save = "DialogSave";

    /// <summary>The unsaved-changes prompt's Don't Save.</summary>
    internal const string DontSave = "DialogDontSave";

    /// <summary>The input's text box.</summary>
    internal const string Input = "DialogInput";

    /// <summary>A block of a message's text, or an alert's message.</summary>
    internal const string Text = "DialogText";

    /// <summary>The text of a message's code pane.</summary>
    internal const string Code = "DialogCode";

    /// <summary>The 📋 after a path.</summary>
    internal const string CopyPath = "DialogCopyPath";

    /// <summary>The ↗ after a link.</summary>
    internal const string OpenLink = "DialogOpenLink";

    /// <summary>A fatal error's or a notice's read-only details box.</summary>
    internal const string Details = "DialogDetails";

    /// <summary>The button that copies those details.</summary>
    internal const string Copy = "DialogCopy";

    /// <summary>The button that closes a fatal error or a notice.</summary>
    internal const string Close = "DialogClose";

    /// <summary>
    /// Numbers the roles a window repeats, so that each id stays unique in it: a role's first control takes
    /// the role's id, and each after it the id and its ordinal, from 2.
    /// </summary>
    internal sealed class Sequence
    {
        private readonly Dictionary<string, int> _counts = new(StringComparer.Ordinal);

        /// <summary>The id of the next control in <paramref name="role"/>.</summary>
        internal string Next(string role)
        {
            int ordinal = _counts.TryGetValue(role, out int count) ? count + 1 : 1;
            _counts[role] = ordinal;
            return ordinal == 1 ? role : role + ordinal.ToString(CultureInfo.InvariantCulture);
        }
    }
}
