using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Bennewitz.Ninja.AppServices.AvaloniaUI.Localization;

namespace Bennewitz.Ninja.AppServices.AvaloniaUI.Dialogs;

/// <summary>
/// The confirmation <see cref="AvaloniaDialogService.ShowConfirmAsync(string, string, string, string)"/>
/// shows: a message, and a confirm and a cancel button, neither of them pressed by Enter.
/// </summary>
/// <remarks>
/// A window class a test builds without an owner or a lifetime, as it builds
/// <see cref="FatalErrorDialog"/>; the service shows it over its owner and returns
/// <see cref="Result"/> (<c>plans/00007</c>, decision 1).
/// </remarks>
internal sealed class ConfirmDialog : Window
{
    /// <summary>Builds the confirmation, unshown.</summary>
    /// <param name="title">The window's title.</param>
    /// <param name="message">The text shown.</param>
    /// <param name="confirmLabel">The confirm button's label.</param>
    /// <param name="cancelLabel">The cancel button's label.</param>
    /// <param name="icon">The window's icon, or <see langword="null"/> to leave the platform's.</param>
    internal ConfirmDialog(string title, string message, string confirmLabel, string cancelLabel, WindowIcon? icon)
    {
        ConfirmButton = new Button { Content = confirmLabel, IsDefault = false, MinWidth = 70 };
        AutomationProperties.SetAutomationId(ConfirmButton, DialogAutomationIds.Confirm);
        ToolTip.SetTip(ConfirmButton, DialogStrings.TipConfirm);
        CancelButton = new Button { Content = cancelLabel, IsCancel = true, MinWidth = 70 };
        AutomationProperties.SetAutomationId(CancelButton, DialogAutomationIds.Cancel);
        ToolTip.SetTip(CancelButton, DialogStrings.TipCancel);

        StackPanel buttonRow = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
        };
        buttonRow.Children.Add(CancelButton);
        buttonRow.Children.Add(ConfirmButton);

        StackPanel body = new() { Margin = new Thickness(16), Spacing = 12 };
        body.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap });
        body.Children.Add(buttonRow);

        Title = title;
        Width = 420;
        MinHeight = 120;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        CanResize = false;
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

    /// <summary>Answers <see langword="true"/> and closes. Enter does not press it.</summary>
    internal Button ConfirmButton { get; }

    /// <summary>Answers <see langword="false"/> and closes; Escape presses it.</summary>
    internal Button CancelButton { get; }

    /// <summary>
    /// The answer once the window has closed.
    /// </summary>
    /// <remarks>
    /// same three-valued semantics as <see cref="MessageConfirmDialog.Result"/>:
    /// null when the user dismisses via X without clicking either button.
    /// </remarks>
    internal bool? Result { get; private set; }
}
