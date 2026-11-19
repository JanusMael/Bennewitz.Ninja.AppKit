using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;

namespace Bennewitz.Ninja.AppServices.AvaloniaUI.Dialogs;

/// <summary>
/// The alert <see cref="AvaloniaDialogService.ShowAlertAsync(string, string)"/> shows, and its overload
/// with an icon: a message the user can select and copy, an OK button, and for an error a header with a
/// ⚠ before the title.
/// </summary>
/// <remarks>
/// A window class a test builds without an owner or a lifetime, as it builds
/// <see cref="FatalErrorDialog"/>; the service shows it over its owner (<c>plans/00007</c>, decision 1).
/// </remarks>
internal sealed class AlertDialog : Window
{
    /// <summary>Builds the alert, unshown.</summary>
    /// <param name="title">The window's title, and the error header's.</param>
    /// <param name="message">The text shown, which the user can select and copy.</param>
    /// <param name="icon">The window's icon, or <see langword="null"/> to leave the platform's.</param>
    /// <param name="isError">Whether to show the error header, and put a ⚠ before the window's title.</param>
    internal AlertDialog(string title, string message, WindowIcon? icon, bool isError)
    {
        OkButton = new Button
        {
            Content = "OK",
            HorizontalAlignment = HorizontalAlignment.Center,
            MinWidth = 70,
            Margin = new Thickness(0, 12, 0, 0),
        };
        AutomationProperties.SetAutomationId(OkButton, DialogAutomationIds.Ok);
        ToolTip.SetTip(OkButton, DialogTips.Ok);

        // Use a SelectableTextBlock so the user can copy the text (e.g. error details).
        // Wrap it in a ScrollViewer so a very long message (e.g. many schema-validation
        // violations) stays scrollable.  NO MaxHeight on the ScrollViewer itself — the
        // DockPanel layout below lets it stretch to fill the window's available height
        // (the Window MaxHeight caps the outer dialog, not the inner viewport).
        SelectableTextBlock textBlock = new()
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
        };
        AutomationProperties.SetAutomationId(textBlock, DialogAutomationIds.Text);
        ToolTip.SetTip(textBlock, DialogTips.SelectText);

        ScrollViewer scrollViewer = new()
        {
            Content = textBlock,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };

        // Switched from StackPanel to DockPanel so OK button docks to the
        // bottom and ScrollViewer fills the remaining area.  Pre-fix: StackPanel +
        // ScrollViewer with MaxHeight=440 meant resizing the dialog vertically left an
        // empty band between the content and the OK button, and the OK button drifted
        // away from the bottom edge.  Pre-fix the horizontal resize also didn't stretch
        // the SelectableTextBlock because StackPanel doesn't constrain child width
        // beyond the longest child's preferred size.
        DockPanel body = new()
        {
            Margin = new Thickness(16),
            LastChildFill = true,
        };

        if (isError)
        {
            // Styled header with a ⚠ icon so error alerts look distinct from info alerts.
            StackPanel header = new()
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Margin = new Thickness(0, 0, 0, 12),
            };
            header.Children.Add(new TextBlock
            {
                Text = "⚠",
                FontSize = 18,
                [!TextBlock.ForegroundProperty] = DialogColors.Brush(DialogColors.ErrorBrush),
                VerticalAlignment = VerticalAlignment.Center,
            });
            header.Children.Add(new TextBlock
            {
                Text = title,
                FontWeight = FontWeight.SemiBold,
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center,
            });
            DockPanel.SetDock(header, Dock.Top);
            body.Children.Add(header);
        }

        // Dock the OK button to the bottom BEFORE the ScrollViewer so LastChildFill
        // assigns the remaining vertical space to the ScrollViewer.  Order matters.
        DockPanel.SetDock(OkButton, Dock.Bottom);
        body.Children.Add(OkButton);
        body.Children.Add(scrollViewer);

        Title = isError ? $"⚠ {title}" : title;
        Width = 520;
        // bumped from 560 → 720 to give users more elbow room when
        // resizing.  Combined with the DockPanel fix above, the ScrollViewer now
        // stretches to fill the available height as the user drags the bottom edge.
        MaxHeight = 720;
        MinHeight = 160;
        MinWidth = 360;
        SizeToContent = SizeToContent.Height;
        CanResize = true;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Content = body;

        if (icon is not null)
        {
            Icon = icon;
        }

        OkButton.Click += (_, _) => Close();
    }

    /// <summary>Closes the alert.</summary>
    internal Button OkButton { get; }
}
