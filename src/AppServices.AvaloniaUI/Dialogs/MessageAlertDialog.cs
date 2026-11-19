using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Bennewitz.Ninja.AppServices.Abstractions.Dialogs;
using Bennewitz.Ninja.AppServices.AvaloniaUI.Localization;

namespace Bennewitz.Ninja.AppServices.AvaloniaUI.Dialogs;

/// <summary>
/// The alert
/// <see cref="AvaloniaDialogService.ShowAlertAsync(string, DialogMessage, DialogCategory)"/> shows: a
/// <see cref="DialogMessage"/>, its category's header and title glyph, and an OK button.
/// </summary>
/// <remarks>
/// A window class a test builds without an owner or a lifetime, as it builds
/// <see cref="FatalErrorDialog"/>; the service shows it over its owner (<c>plans/00007</c>, decision 1).
/// </remarks>
internal sealed class MessageAlertDialog : Window
{
    /// <summary>Builds the alert, unshown.</summary>
    /// <param name="title">The window's title, after its category's glyph, and the header's.</param>
    /// <param name="message">The message shown, its paths, links and code included.</param>
    /// <param name="category">Chooses the header and the title's glyph.</param>
    /// <param name="icon">The window's icon, or <see langword="null"/> to leave the platform's.</param>
    internal MessageAlertDialog(string title, DialogMessage message, DialogCategory category, WindowIcon? icon)
    {
        OkButton = new Button
        {
            Content = DialogStrings.LabelOk,
            HorizontalAlignment = HorizontalAlignment.Center,
            MinWidth = 70,
            Margin = new Thickness(0, 12, 0, 0),
        };
        AutomationProperties.SetAutomationId(OkButton, DialogAutomationIds.Ok);
        ToolTip.SetTip(OkButton, DialogStrings.TipOk);

        Title = DialogChrome.TitleWithGlyph(title, category);
        Width = 520;
        // bumped from 560 → 720 + introduced MinHeight/MinWidth so
        // the resize handles have a sensible operating range.  Combined with the
        // DockPanel below, the ScrollViewer stretches to fill the available
        // height as the user drags the bottom edge.
        MaxHeight = 720;
        MinHeight = 160;
        MinWidth = 360;
        SizeToContent = SizeToContent.Height;
        CanResize = true;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        // Use the segment-grouping content helper so Code segments
        // get a framed bordered pane while the explanatory Text segments around
        // them render as plain inline runs.  Pre-fix attempt wrapped the WHOLE
        // content (including the "These errors are in the loaded files..."
        // explanatory paragraph) in the framed pane, which the user pointed out
        // was wrong: only the actual code block should be inside the frame.
        Control messageView = DialogMessageContent.BuildSegmentedDialogContent(message, this);

        // No MaxHeight on the ScrollViewer — the DockPanel layout lets it stretch to
        // fill the window's available height (Window MaxHeight caps the outer dialog).
        ScrollViewer scrollViewer = new()
        {
            Content = messageView,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };

        // Switched from StackPanel to DockPanel so OK button docks to
        // the bottom and ScrollViewer fills the remaining area.  Pre-fix: StackPanel
        // + ScrollViewer with fixed MaxHeight=440 meant the schema-validation dialog
        // left an empty band between the error list and the OK button when resized
        // vertically, and the SelectableTextBlock didn't stretch horizontally on a
        // bottom-corner drag.
        DockPanel body = new()
        {
            Margin = new Thickness(16),
            LastChildFill = true,
        };

        // Category header docks Top; OK button docks Bottom; ScrollViewer is the
        // last child and fills remaining space.  Order matters for DockPanel.
        DialogChrome.AppendCategoryHeader(body, title, category); // appends with Dock=Top below
        DockPanel.SetDock(OkButton, Dock.Bottom);
        body.Children.Add(OkButton);
        body.Children.Add(scrollViewer);

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
