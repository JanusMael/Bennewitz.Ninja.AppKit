using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Bennewitz.Ninja.AppServices.Abstractions;
using Bennewitz.Ninja.AppServices.Abstractions.Dialogs;

namespace Bennewitz.Ninja.AppServices.AvaloniaUI.Dialogs;

/// <summary>
/// The prompt <see cref="AvaloniaDialogService.ShowUnsavedChangesAsync(string, DialogMessage)"/> shows
/// when a window with unsaved changes is closed: a <see cref="DialogMessage"/>, and Don't Save, Cancel and
/// Save buttons.
/// </summary>
/// <remarks>
/// A window class a test builds without an owner or a lifetime, as it builds
/// <see cref="FatalErrorDialog"/>; the service shows it over its owner and returns
/// <see cref="Result"/> (<c>plans/00007</c>, decision 1).
/// </remarks>
internal sealed class UnsavedChangesDialog : Window
{
    /// <summary>Builds the prompt, unshown.</summary>
    /// <param name="title">The window's title.</param>
    /// <param name="message">The message shown, its paths and links included.</param>
    /// <param name="icon">The window's icon, or <see langword="null"/> to leave the platform's.</param>
    internal UnsavedChangesDialog(string title, DialogMessage message, WindowIcon? icon)
    {
        // Save is the safe default (Enter triggers it): users who reflexively
        // hit Enter after clicking close should save, not lose work.
        SaveButton = new Button { Content = "Save", IsDefault = true, MinWidth = 90 };
        // Don't Save is destructive — explicitly NOT default so Enter doesn't
        // accidentally discard work.  No special key binding.
        DontSaveButton = new Button { Content = "Don't Save", MinWidth = 90 };
        DialogChrome.ApplyDestructiveStyle(DontSaveButton, DialogCategory.Destructive);
        // Cancel keeps the window open.  IsCancel = true wires Escape.
        CancelButton = new Button { Content = "Cancel", IsCancel = true, MinWidth = 90 };

        StackPanel buttonRow = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
        };
        // Left-to-right reading order: destructive on the far left so the
        // user has to deliberately track past Cancel and Save to reach it.
        buttonRow.Children.Add(DontSaveButton);
        buttonRow.Children.Add(CancelButton);
        buttonRow.Children.Add(SaveButton);

        Title = DialogChrome.TitleWithGlyph(title, DialogCategory.Confirmation);
        Width = 480;
        MinHeight = 140;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        CanResize = false;

        StackPanel body = new() { Margin = new Thickness(16), Spacing = 12 };
        DialogChrome.AppendCategoryHeader(body, title, DialogCategory.Confirmation);
        body.Children.Add(DialogMessageContent.BuildSegmentedTextBlock(message, this));
        body.Children.Add(buttonRow);
        Content = body;

        if (icon is not null)
        {
            Icon = icon;
        }

        SaveButton.Click += (_, _) =>
        {
            Result = UnsavedChangesChoice.Save;
            Close();
        };
        DontSaveButton.Click += (_, _) =>
        {
            Result = UnsavedChangesChoice.DontSave;
            Close();
        };
        CancelButton.Click += (_, _) =>
        {
            Result = UnsavedChangesChoice.Cancel;
            Close();
        };
    }

    /// <summary>Answers <see cref="UnsavedChangesChoice.Save"/> and closes; Enter presses it.</summary>
    internal Button SaveButton { get; }

    /// <summary>Answers <see cref="UnsavedChangesChoice.DontSave"/> and closes; no key presses it.</summary>
    internal Button DontSaveButton { get; }

    /// <summary>Answers <see cref="UnsavedChangesChoice.Cancel"/> and closes; Escape presses it.</summary>
    internal Button CancelButton { get; }

    /// <summary>
    /// The answer once the window has closed.
    /// </summary>
    /// <remarks>
    /// three-button modal for the window-close-with-edits
    /// prompt.  X-close (no button click) collapses to Cancel = "keep
    /// window open" per the X-never-proceeds principle: no button click fired,
    /// the result stays Cancel.
    /// </remarks>
    internal UnsavedChangesChoice Result { get; private set; } = UnsavedChangesChoice.Cancel;
}
