using Avalonia.Controls;
using Bennewitz.Ninja.AppServices.Abstractions.Dialogs;
using Bennewitz.Ninja.AppServices.AvaloniaUI.Dialogs;

namespace AppServices.Tests.Dialogs;

/// <summary>
/// The table of windows: every window the Avalonia package builds, with the arguments that build it and
/// the automation id of every interactive control in it (<c>plans/00007</c>, decision 4).
/// </summary>
/// <remarks>
/// <para>
/// Each window that takes a <see cref="DialogMessage"/> is built with <see cref="Message"/>, which holds
/// two paths, two links and a code block, so every kind of control a message adds is in it and every role
/// it repeats is numbered. A window class the package declares that is missing here fails
/// <see cref="DialogAccessibilityTests.Every_window_class_the_package_declares_is_in_the_table"/>.
/// </para>
/// <para>
/// Every string a row passes is one of this class's, and <see cref="Given"/> lists them, so that
/// <see cref="DialogStringsTests"/> tells the caller's words in a window from the package's.
/// </para>
/// </remarks>
internal static class DialogWindows
{
    /// <summary>Every window's title.</summary>
    internal const string Title = "Title";

    /// <summary>The message of a window that takes a string for one.</summary>
    internal const string MessageText = "Message";

    /// <summary>A confirmation's confirm label, as its caller passes it.</summary>
    internal const string ConfirmLabel = "Delete";

    /// <summary>A confirmation's cancel label, as its caller passes it.</summary>
    internal const string CancelLabel = "Keep";

    /// <summary>The input dialog's prompt.</summary>
    internal const string Prompt = "Name the new profile";

    /// <summary>The input dialog's placeholder.</summary>
    internal const string Placeholder = "Profile name";

    /// <summary>The notice dialog's body.</summary>
    internal const string NoticeBody = "line 1\nline 2";

    /// <summary>The message of the exception the fatal-error dialog shows.</summary>
    internal const string Failure = "boom";

    /// <summary>The first path in <see cref="Message"/>.</summary>
    internal const string FirstPath = @"C:\data\notes.txt";

    /// <summary>The second path in <see cref="Message"/>.</summary>
    internal const string SecondPath = @"C:\data\backup.txt";

    /// <summary>The first link in <see cref="Message"/>.</summary>
    internal const string FirstLink = "https://example.com/guide";

    /// <summary>The second link in <see cref="Message"/>.</summary>
    internal const string SecondLink = "https://example.com/questions";

    /// <summary>The text of the first link in <see cref="Message"/>.</summary>
    internal const string FirstLinkText = "the guide";

    /// <summary>The text of the second link in <see cref="Message"/>.</summary>
    internal const string SecondLinkText = "the questions";

    /// <summary>The windows, each with what builds it and the ids its interactive controls carry.</summary>
    internal static IReadOnlyList<Entry> All { get; } =
    [
        new(typeof(AlertDialog),
            () => new AlertDialog(Title, MessageText, icon: null, isError: true),
            ["DialogOk", "DialogText"]),
        new(typeof(MessageAlertDialog),
            () => new MessageAlertDialog(Title, Message(), DialogCategory.Error, icon: null),
            ["DialogOk", "DialogText", "DialogCopyPath", "DialogCopyPath2", "DialogOpenLink", "DialogOpenLink2",
             "DialogCode", "DialogText2"]),
        new(typeof(ConfirmDialog),
            () => new ConfirmDialog(Title, MessageText, ConfirmLabel, CancelLabel, icon: null),
            ["DialogConfirm", "DialogCancel"]),
        new(typeof(MessageConfirmDialog),
            () => new MessageConfirmDialog(Title, Message(), DialogCategory.Destructive, ConfirmLabel, CancelLabel, icon: null),
            ["DialogConfirm", "DialogCancel", "DialogText", "DialogCopyPath", "DialogCopyPath2", "DialogOpenLink",
             "DialogOpenLink2", "DialogCode", "DialogText2"]),
        new(typeof(InputDialog),
            () => new InputDialog(Title, Prompt, Placeholder, icon: null),
            ["DialogInput", "DialogOk", "DialogCancel"]),
        new(typeof(UnsavedChangesDialog),
            () => new UnsavedChangesDialog(Title, Message(), icon: null),
            ["DialogSave", "DialogDontSave", "DialogCancel", "DialogText", "DialogCopyPath", "DialogCopyPath2",
             "DialogOpenLink", "DialogOpenLink2"]),
        new(typeof(FatalErrorDialog),
            () => new FatalErrorDialog(Title, MessageText, new InvalidOperationException(Failure)),
            ["DialogDetails", "DialogCopy", "DialogClose"]),
        new(typeof(NonFatalNoticeDialog),
            () => new NonFatalNoticeDialog(Title, MessageText, NoticeBody),
            ["DialogDetails", "DialogCopy", "DialogClose"]),
    ];

    /// <summary>The windows that show a <see cref="DialogMessage"/>, and so its 📋 and ↗ buttons.</summary>
    internal static IReadOnlyList<string> WithMessages { get; } =
        [nameof(MessageAlertDialog), nameof(MessageConfirmDialog), nameof(UnsavedChangesDialog)];

    /// <summary>
    /// Every string a row passes its window, and every text and link in <see cref="Message"/>, also as a
    /// text block shows one that follows a code block, its leading line breaks gone.
    /// </summary>
    internal static IReadOnlyList<string> Given { get; } =
    [
        .. new[] { Title, MessageText, ConfirmLabel, CancelLabel, Prompt, Placeholder, NoticeBody, Failure }
            .Concat(Message().Segments.SelectMany(segment => new[]
            {
                segment.Value, segment.Value.TrimStart('\r', '\n'), segment.Url ?? string.Empty,
            }))
            .Where(text => text.Length > 0)
            .Distinct(StringComparer.Ordinal),
    ];

    /// <summary>The entry for the window class named <paramref name="name"/>.</summary>
    internal static Entry Named(string name)
    {
        return All.Single(entry => entry.Type.Name == name);
    }

    /// <summary>
    /// A message holding two paths, two links and a code block, with a line of text that holds no button
    /// before the code.
    /// </summary>
    internal static DialogMessage Message()
    {
        return DialogMessage.Builder()
                            .Text("Saved ").Path(FirstPath)
                            .Text(" and ").Path(SecondPath)
                            .Text("; read ").Hyperlink(FirstLinkText, FirstLink)
                            .Text(" or ").Hyperlink(SecondLinkText, SecondLink)
                            .Text(".\nThis line holds no button.")
                            .Code("{ \"answer\": 42 }")
                            .Text("\n\nNothing else changed.")
                            .Build();
    }

    /// <summary>A window class the package declares, what builds it, and the ids of its interactive controls.</summary>
    /// <param name="Type">The window's class.</param>
    /// <param name="Build">Builds the window, unshown.</param>
    /// <param name="Ids">The automation id of each interactive control in it, in no particular order.</param>
    internal sealed record Entry(Type Type, Func<Window> Build, string[] Ids);
}
