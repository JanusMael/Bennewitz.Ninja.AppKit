using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Bennewitz.Ninja.AppServices.Abstractions.Dialogs;

namespace Bennewitz.Ninja.AppServices.AvaloniaUI.Dialogs;

/// <summary>
/// The confirmation
/// <see cref="AvaloniaDialogService.ShowConfirmAsync(string, DialogMessage, DialogCategory, string, string)"/>
/// shows: a <see cref="DialogMessage"/>, its category's header and title glyph, and a confirm and a
/// cancel button, the confirm button filled red for a destructive action.
/// </summary>
/// <remarks>
/// A window class a test builds without an owner or a lifetime, as it builds
/// <see cref="FatalErrorDialog"/>; the service shows it over its owner and returns
/// <see cref="Result"/> (<c>plans/00007</c>, decision 1).
/// </remarks>
internal sealed class MessageConfirmDialog : Window
{
    /// <summary>Builds the confirmation, unshown.</summary>
    /// <param name="title">The window's title, after its category's glyph, and the header's.</param>
    /// <param name="message">The message shown, its paths, links and code included.</param>
    /// <param name="category">Chooses the header, the title's glyph, and whether Enter confirms.</param>
    /// <param name="confirmLabel">The confirm button's label.</param>
    /// <param name="cancelLabel">The cancel button's label.</param>
    /// <param name="icon">The window's icon, or <see langword="null"/> to leave the platform's.</param>
    internal MessageConfirmDialog(
        string title,
        DialogMessage message,
        DialogCategory category,
        string confirmLabel,
        string cancelLabel,
        WindowIcon? icon)
    {
        ConfirmButton = new Button
        {
            Content = confirmLabel,
            // Enter triggers Confirm on safe categories
            // (Information, Confirmation).  Destructive prompts keep
            // IsDefault=false so Enter doesn't accidentally Delete /
            // Save-anyway / etc. — the user must explicitly click.
            // Escape always triggers Cancel via the cancel button's
            // IsCancel=true.
            IsDefault = category != DialogCategory.Destructive,
            MinWidth = 70,
        };
        DialogChrome.ApplyDestructiveStyle(ConfirmButton, category);
        AutomationProperties.SetAutomationId(ConfirmButton, DialogAutomationIds.Confirm);
        ToolTip.SetTip(ConfirmButton, DialogTips.Confirm);

        CancelButton = new Button { Content = cancelLabel, IsCancel = true, MinWidth = 70 };
        AutomationProperties.SetAutomationId(CancelButton, DialogAutomationIds.Cancel);
        ToolTip.SetTip(CancelButton, DialogTips.Cancel);

        StackPanel buttonRow = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
        };
        buttonRow.Children.Add(CancelButton);
        buttonRow.Children.Add(ConfirmButton);

        Title = DialogChrome.TitleWithGlyph(title, category);
        Width = 480;
        MinHeight = 120;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        CanResize = false;

        StackPanel body = new() { Margin = new Thickness(16), Spacing = 12 };
        DialogChrome.AppendCategoryHeader(body, title, category);
        // Use the segment-grouping helper so a Code segment
        // (e.g. schema-validation error list in the save-time "Save anyway?"
        // confirm) renders inside a framed pane while explanatory Text
        // around it stays as plain inline runs.  See MessageAlertDialog's
        // comment for the rationale.
        body.Children.Add(DialogMessageContent.BuildSegmentedDialogContent(message, this));
        body.Children.Add(buttonRow);
        Content = body;

        if (icon is not null)
        {
            Icon = icon;
        }

        ConfirmButton.Click += (_, _) =>
        {
            Result = true;
            Close();
        };
        CancelButton.Click += (_, _) =>
        {
            Result = false;
            Close();
        };
    }

    /// <summary>Answers <see langword="true"/> and closes; Enter presses it unless the category is destructive.</summary>
    internal Button ConfirmButton { get; }

    /// <summary>Answers <see langword="false"/> and closes; Escape presses it.</summary>
    internal Button CancelButton { get; }

    /// <summary>
    /// The answer once the window has closed.
    /// </summary>
    /// <remarks>
    /// three-valued result: null when the user dismisses
    /// via the window-close (X) without clicking either button.
    /// Callers MUST treat null as "abort whatever flow this dialog
    /// was a step of," not as a synonym for the cancel-side action.
    /// Concrete state machine:
    /// <code>
    ///   confirmButton.Click → result = true   → close
    ///   cancelButton.Click  → result = false  → close   (also Escape, via IsCancel)
    ///   X (window-manager)  → result stays null → close
    /// </code>
    /// </remarks>
    internal bool? Result { get; private set; }
}
