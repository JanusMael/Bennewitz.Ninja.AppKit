using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Bennewitz.Ninja.AppServices.Abstractions.Dialogs;

namespace Bennewitz.Ninja.AppServices.AvaloniaUI.Dialogs;

/// <summary>
/// What a dialog's <see cref="DialogCategory"/> adds to it: a glyph before the window's title, a header
/// above the message, and the filled style of a destructive button.
/// </summary>
/// <remarks>
/// <para>
/// Moved out of <see cref="AvaloniaDialogService"/> with the dialogs that take a category
/// (<c>plans/00007</c>, decision 1). Its colours are <see cref="DialogColors"/>' keys (decision 6).
/// </para>
/// <para>
/// A theme repaints a button's <c>PART_ContentPresenter</c> when it is pointed at and pressed, whatever the
/// button's own fill: under Semi in Light a destructive button's white label all but vanishes then, and
/// Fluent paints the label dark. So the window carries styles of its own for those two states, which paint
/// the presenter's fill and label from the destructive keys. They win over the theme's, since a window's
/// styles are applied after the application's; a theme whose button names its presenter otherwise keeps its
/// own colours there.
/// </para>
/// </remarks>
internal static class DialogChrome
{
    /// <summary>The class of a destructive button, which its window's styles paint pointed at and pressed.</summary>
    internal const string DestructiveButtonClass = "dialog-destructive";

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

        (string glyph, string colour) = category == DialogCategory.Error
            ? ("⛔", DialogColors.ErrorBrush)
            : ("⚠", DialogColors.WarningBrush);

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
            [!TextBlock.ForegroundProperty] = DialogColors.Brush(colour),
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
    /// Fill <paramref name="confirmButton"/> red behind a white label when the category is
    /// <see cref="DialogCategory.Destructive"/>, and add to <paramref name="dialogWindow"/> the styles that
    /// keep it so pointed at and pressed. Other categories leave the button at the theme's style.
    /// </summary>
    internal static void ApplyDestructiveStyle(Window dialogWindow, Button confirmButton, DialogCategory category)
    {
        if (category != DialogCategory.Destructive)
        {
            return;
        }

        confirmButton[!TemplatedControl.BackgroundProperty] = DialogColors.Brush(DialogColors.DestructiveBackgroundBrush);
        confirmButton[!TemplatedControl.ForegroundProperty] = DialogColors.Brush(DialogColors.DestructiveForegroundBrush);
        confirmButton.FontWeight = FontWeight.SemiBold;
        confirmButton.Classes.Add(DestructiveButtonClass);

        // Pointed at, then pressed: a press by the mouse is pointed at as well, and of two styles that both
        // match, the later wins.
        dialogWindow.Styles.Add(PaintedWhen(":pointerover", DialogColors.DestructivePointerOverBackgroundBrush));
        dialogWindow.Styles.Add(PaintedWhen(":pressed", DialogColors.DestructivePressedBackgroundBrush));
    }

    /// <summary>
    /// A style that paints a destructive button's presenter with <paramref name="fill"/> and the destructive
    /// label in <paramref name="pseudoClass"/>.
    /// </summary>
    private static Style PaintedWhen(string pseudoClass, string fill)
    {
        Style style = new(selector => selector
            .OfType<Button>()
            .Class(DestructiveButtonClass)
            .Class(pseudoClass)
            .Template()
            .OfType<ContentPresenter>()
            .Name("PART_ContentPresenter"));
        style.Setters.Add(new Setter(ContentPresenter.BackgroundProperty, DialogColors.Brush(fill)));
        style.Setters.Add(new Setter(
            ContentPresenter.ForegroundProperty, DialogColors.Brush(DialogColors.DestructiveForegroundBrush)));
        return style;
    }
}
