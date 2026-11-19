using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Bennewitz.Ninja.AppServices.Abstractions.Dialogs;

namespace Bennewitz.Ninja.AppServices.AvaloniaUI.Dialogs;

/// <summary>
/// What a dialog's <see cref="DialogCategory"/> adds to it: a glyph before the window's title, a header
/// above the message, and the filled style of a destructive button.
/// </summary>
/// <remarks>
/// Moved out of <see cref="AvaloniaDialogService"/> with the dialogs that take a category
/// (<c>plans/00007</c>, decision 1); what it builds is unchanged.
/// </remarks>
internal static class DialogChrome
{
    /// <summary>
    /// Prepend the title with a category-appropriate glyph for the window
    /// chrome.  Returns the title unchanged for neutral categories.
    /// </summary>
    internal static string TitleWithGlyph(string title, DialogCategory category)
    {
        return category switch
        {
            DialogCategory.Destructive => $"⚠ {title}",
            DialogCategory.Error => $"⛔ {title}",
            var _ => title,
        };
    }

    /// <summary>
    /// Add a styled header strip for categories that warrant visual
    /// elevation (Destructive, Error).  No-ops for neutral categories so
    /// confirm/info dialogs keep their existing minimal look.
    /// </summary>
    internal static void AppendCategoryHeader(Panel body, string title, DialogCategory category)
    {
        if (category is not (DialogCategory.Destructive or DialogCategory.Error))
        {
            return;
        }

        (string glyph, string color) = category switch
        {
            DialogCategory.Destructive => ("⚠", "#C77700"),
            DialogCategory.Error => ("⛔", "#C62828"),
            var _ => ("", "#888888"),
        };

        StackPanel header = new()
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            // Bottom margin so the header doesn't kiss the ScrollViewer.  Read by
            // both StackPanel (vertical stack) and DockPanel (Top-docked child).
            Margin = new Thickness(0, 0, 0, 12),
        };
        header.Children.Add(new TextBlock
        {
            Text = glyph,
            FontSize = 18,
            Foreground = new SolidColorBrush(Color.Parse(color)),
            VerticalAlignment = VerticalAlignment.Center,
        });
        header.Children.Add(new TextBlock
        {
            Text = title,
            FontWeight = FontWeight.SemiBold,
            FontSize = 14,
            VerticalAlignment = VerticalAlignment.Center,
        });
        // Set Dock.Top so the header docks correctly when `body` is a
        // DockPanel (rich ShowAlertAsync).  StackPanel ignores the Dock attached
        // property, so this is harmless on the legacy stack-based callers.
        DockPanel.SetDock(header, Dock.Top);
        body.Children.Add(header);
    }

    /// <summary>
    /// Apply danger styling to the confirm button when the category is
    /// <see cref="DialogCategory.Destructive"/>.  Other categories leave the
    /// button at the default style.
    /// </summary>
    internal static void ApplyDestructiveStyle(Button confirmButton, DialogCategory category)
    {
        if (category != DialogCategory.Destructive)
        {
            return;
        }

        confirmButton.Background = new SolidColorBrush(Color.Parse("#C62828"));
        confirmButton.Foreground = Brushes.White;
        confirmButton.FontWeight = FontWeight.SemiBold;
    }
}
