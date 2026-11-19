using Avalonia.Controls;
using Bennewitz.Ninja.AppServices.Abstractions.Dialogs;
using Bennewitz.Ninja.AppServices.AvaloniaUI.Dialogs;

namespace AppServices.Tests.Dialogs;

/// <summary>
/// The table of windows: every window the Avalonia package builds, with the arguments that build it and
/// the automation id of every interactive control in it (<c>plans/00007</c>, decision 4).
/// </summary>
/// <remarks>
/// Each window that takes a <see cref="DialogMessage"/> is built with <see cref="Message"/>, which holds
/// two paths, two links and a code block, so every kind of control a message adds is in it and every role
/// it repeats is numbered. A window class the package declares that is missing here fails
/// <see cref="DialogAccessibilityTests.Every_window_class_the_package_declares_is_in_the_table"/>.
/// </remarks>
internal static class DialogWindows
{
    /// <summary>The first path in <see cref="Message"/>.</summary>
    internal const string FirstPath = @"C:\data\notes.txt";

    /// <summary>The second path in <see cref="Message"/>.</summary>
    internal const string SecondPath = @"C:\data\backup.txt";

    /// <summary>The first link in <see cref="Message"/>.</summary>
    internal const string FirstLink = "https://example.com/guide";

    /// <summary>The second link in <see cref="Message"/>.</summary>
    internal const string SecondLink = "https://example.com/questions";

    /// <summary>The windows, each with what builds it and the ids its interactive controls carry.</summary>
    internal static IReadOnlyList<Entry> All { get; } =
    [
        new(typeof(AlertDialog),
            () => new AlertDialog("Title", "Message", icon: null, isError: true),
            ["DialogOk", "DialogText"]),
        new(typeof(MessageAlertDialog),
            () => new MessageAlertDialog("Title", Message(), DialogCategory.Error, icon: null),
            ["DialogOk", "DialogText", "DialogCopyPath", "DialogCopyPath2", "DialogOpenLink", "DialogOpenLink2",
             "DialogCode", "DialogText2"]),
        new(typeof(ConfirmDialog),
            () => new ConfirmDialog("Title", "Message", "Delete", "Keep", icon: null),
            ["DialogConfirm", "DialogCancel"]),
        new(typeof(MessageConfirmDialog),
            () => new MessageConfirmDialog("Title", Message(), DialogCategory.Destructive, "Delete", "Keep", icon: null),
            ["DialogConfirm", "DialogCancel", "DialogText", "DialogCopyPath", "DialogCopyPath2", "DialogOpenLink",
             "DialogOpenLink2", "DialogCode", "DialogText2"]),
        new(typeof(InputDialog),
            () => new InputDialog("Title", "Name the new profile", "Profile name", icon: null),
            ["DialogInput", "DialogOk", "DialogCancel"]),
        new(typeof(UnsavedChangesDialog),
            () => new UnsavedChangesDialog("Title", Message(), icon: null),
            ["DialogSave", "DialogDontSave", "DialogCancel", "DialogText", "DialogCopyPath", "DialogCopyPath2",
             "DialogOpenLink", "DialogOpenLink2"]),
        new(typeof(FatalErrorDialog),
            () => new FatalErrorDialog("Title", "Message", new InvalidOperationException("boom")),
            ["DialogDetails", "DialogCopy", "DialogClose"]),
        new(typeof(NonFatalNoticeDialog),
            () => new NonFatalNoticeDialog("Title", "Message", "line 1\nline 2"),
            ["DialogDetails", "DialogCopy", "DialogClose"]),
    ];

    /// <summary>The windows that show a <see cref="DialogMessage"/>, and so its 📋 and ↗ buttons.</summary>
    internal static IReadOnlyList<string> WithMessages { get; } =
        [nameof(MessageAlertDialog), nameof(MessageConfirmDialog), nameof(UnsavedChangesDialog)];

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
                            .Text("; read ").Hyperlink("the guide", FirstLink)
                            .Text(" or ").Hyperlink("the questions", SecondLink)
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
