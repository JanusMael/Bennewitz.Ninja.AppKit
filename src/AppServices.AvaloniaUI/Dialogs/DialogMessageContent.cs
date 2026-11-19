using System.Diagnostics;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Styling;
using Bennewitz.Ninja.AppServices.Abstractions.Dialogs;
using Serilog;

namespace Bennewitz.Ninja.AppServices.AvaloniaUI.Dialogs;

/// <summary>
/// Renders a <see cref="DialogMessage"/> into the controls a dialog shows it in: text, bold text, a path
/// with its 📋, a link with its ↗, and code in a framed pane.
/// </summary>
/// <remarks>
/// <para>
/// Moved out of <see cref="AvaloniaDialogService"/> with the dialogs that show a message
/// (<c>plans/00007</c>, decision 1).
/// </para>
/// <para>
/// Each block of text is a <see cref="SelectableTextBlock"/>, named by its text, as its automation peer
/// names it, with a tooltip that says it can be selected and copied (decision 4). The 📋 after a path and
/// the ↗ after a link are buttons in the block's line (decision 5): Tab reaches each, Enter and Space press
/// it, and UI Automation invokes it. Each keeps the look of the text glyph it replaced: it has no border,
/// no padding and no minimum size, its fill is transparent, and the window's styles keep the theme from
/// filling its <c>PART_ContentPresenter</c> when it is pointed at or pressed. Keyboard focus still draws
/// the theme's ring.
/// </para>
/// <para>
/// ⚠ The fill is transparent, never null: a null fill takes no pointer, and on the headless platform
/// neither does text, so a glyph with no fill could not be clicked there, and a fill cleared to null on
/// pointer-over would let go of the pointer the moment it arrived (measured on 2026-11-19).
/// </para>
/// </remarks>
internal static class DialogMessageContent
{
    /// <summary>The class of the 📋 and ↗ buttons, which the window's styles keep unfilled.</summary>
    internal const string GlyphButtonClass = "dialog-glyph";

    /// <summary>
    /// Hands the link of a pressed ↗ to the user's browser: the platform shell, unless a test replaces it,
    /// and <see cref="ResetForTests"/> puts it back.
    /// </summary>
    internal static Action<string> OpenLink { get; set; } = OpenWithShell;

    /// <summary>Puts <see cref="OpenLink"/> back. Test-only.</summary>
    internal static void ResetForTests()
    {
        OpenLink = OpenWithShell;
    }

    /// <summary>
    /// Render a <see cref="DialogMessage"/>'s segments into a
    /// <see cref="SelectableTextBlock"/>'s inline collection.  Path segments
    /// are rendered monospace + accent and followed by a 📋 button that
    /// copies the path to the clipboard; hyperlink segments render accent +
    /// underline and are followed by a ↗ button that opens the URL in the
    /// user's default browser.
    /// </summary>
    internal static SelectableTextBlock BuildSegmentedTextBlock(DialogMessage message, Window dialogWindow)
    {
        KeepGlyphButtonsUnfilled(dialogWindow);
        return BuildSegmentedTextBlockFor(message.Segments, dialogWindow, new DialogAutomationIds.Sequence(), isCode: false);
    }

    /// <summary>
    /// Render an arbitrary <see cref="DialogSegment"/> sequence into a
    /// SelectableTextBlock.  Extracted from <see cref="BuildSegmentedTextBlock"/>
    /// so the segment-grouping helper (<see cref="BuildSegmentedDialogContent"/>)
    /// can call it per-group without having to construct a synthetic
    /// <see cref="DialogMessage"/>.
    /// </summary>
    private static SelectableTextBlock BuildSegmentedTextBlockFor(
        IEnumerable<DialogSegment> segments, Window dialogWindow, DialogAutomationIds.Sequence ids, bool isCode)
    {
        SelectableTextBlock tb = new()
        {
            TextWrapping = TextWrapping.Wrap,
        };
        AutomationProperties.SetAutomationId(tb, ids.Next(isCode ? DialogAutomationIds.Code : DialogAutomationIds.Text));
        ToolTip.SetTip(tb, isCode ? DialogTips.SelectCode : DialogTips.SelectText);

        foreach (DialogSegment seg in segments)
        {
            switch (seg.Kind)
            {
                case DialogSegmentKind.Text:
                    tb.Inlines!.Add(new Run(seg.Value));
                    break;

                case DialogSegmentKind.Bold:
                    tb.Inlines!.Add(new Run(seg.Value) { FontWeight = FontWeight.SemiBold });
                    break;

                case DialogSegmentKind.Path:
                    AppendPathRun(tb, seg.Value, dialogWindow, ids);
                    break;

                case DialogSegmentKind.Hyperlink:
                    AppendHyperlinkRun(tb, seg.Value, seg.Url ?? string.Empty, ids);
                    break;

                case DialogSegmentKind.Code:
                    // Monospace family matches the Path segment's family so
                    // the dialog has visual consistency between inline paths
                    // and embedded code/JSON blocks.  No accent colour — the
                    // monospace shift alone signals "this is code".
                    tb.Inlines!.Add(new Run(seg.Value)
                    {
                        FontFamily = new FontFamily("Consolas,Menlo,monospace"),
                    });
                    break;
            }
        }

        return tb;
    }

    /// <summary>
    /// Build dialog content where consecutive <see cref="DialogSegmentKind.Code"/>
    /// segments render inside a framed bordered pane (theme-aware bg via
    /// <c>AppCodeBlockBackgroundBrush</c> / <c>AppCodeBlockBorderBrush</c>) and
    /// all other segments stay as plain inline runs.  Two cases:
    /// <list type="bullet">
    ///   <item>Message has NO Code segments → returns a single
    ///   <see cref="SelectableTextBlock"/> with all segments inlined (same as
    ///   the pre-fix <see cref="BuildSegmentedTextBlock"/> behaviour, so dialogs
    ///   that don't use <c>Code(...)</c> render identically).</item>
    ///   <item>Message has at least one Code segment → returns a vertical
    ///   <see cref="StackPanel"/> with one element per consecutive same-kind
    ///   group: framed <see cref="Border"/> for Code groups, plain
    ///   <see cref="SelectableTextBlock"/> for everything else.  This keeps
    ///   explanatory text ABOVE and BELOW the framed code OUTSIDE the frame,
    ///   matching the user-feedback contract: "only the errors should be in
    ///   their own border."</item>
    /// </list>
    /// </summary>
    internal static Control BuildSegmentedDialogContent(DialogMessage message, Window dialogWindow)
    {
        KeepGlyphButtonsUnfilled(dialogWindow);
        DialogAutomationIds.Sequence ids = new();

        // Fast-path: no Code segments → identical to the legacy inline renderer.
        // Keeps every other dialog visually unchanged.
        if (!message.Segments.Any(s => s.Kind == DialogSegmentKind.Code))
        {
            return BuildSegmentedTextBlockFor(message.Segments, dialogWindow, ids, isCode: false);
        }

        List<(bool IsCode, List<DialogSegment> Segments)> groups = GroupSegmentsByKind(message.Segments);
        StackPanel stack = new() { Spacing = 12 };
        for (int i = 0; i < groups.Count; i++)
        {
            (bool isCode, List<DialogSegment> segs) = groups[i];
            if (isCode)
            {
                stack.Children.Add(BuildCodePane(segs, dialogWindow, ids));
            }
            else
            {
                bool precededByCode = i > 0 && groups[i - 1].IsCode;
                List<DialogSegment> effectiveSegs = precededByCode ? TrimLeadingWhitespaceAfterCode(segs) : segs;
                stack.Children.Add(BuildSegmentedTextBlockFor(effectiveSegs, dialogWindow, ids, isCode: false));
            }
        }

        return stack;
    }

    /// <summary>
    /// Group consecutive <see cref="DialogSegment"/>s by whether they're
    /// <see cref="DialogSegmentKind.Code"/>.  A single <c>.Code()</c> call
    /// yields one segment; multiple consecutive <c>.Code()</c> calls are
    /// rare but treated as one group.  Non-Code groups can mix
    /// Text / Bold / Path / Hyperlink and render inline as before.
    /// </summary>
    private static List<(bool IsCode, List<DialogSegment> Segments)> GroupSegmentsByKind(
        IReadOnlyList<DialogSegment> segments)
    {
        List<(bool IsCode, List<DialogSegment> Segments)> groups = new();
        foreach (DialogSegment seg in segments)
        {
            bool isCode = seg.Kind == DialogSegmentKind.Code;
            if (groups.Count == 0 || groups[^1].IsCode != isCode)
            {
                groups.Add((isCode, new List<DialogSegment>()));
            }

            groups[^1].Segments.Add(seg);
        }

        return groups;
    }

    /// <summary>
    /// Trim leading "\n" from non-Code groups when the previous
    /// group was a Code group.  The schema-validation message template is
    /// <c>.Code(errors) .Text("\n\n") .Text("...")</c> — the "\n\n" was there
    /// to vertically separate the code block from the explanatory paragraph
    /// when they shared one TextBlock.  Now that Code lives in its own framed
    /// pane and Text lives in a separate <see cref="SelectableTextBlock"/>
    /// below, the explicit newlines would render as a tall empty gap above
    /// the explanatory paragraph.  StackPanel <c>Spacing=12</c> handles the
    /// separation visually, so strip leading whitespace from the first
    /// Text segment when its group follows a Code group.
    /// </summary>
    private static List<DialogSegment> TrimLeadingWhitespaceAfterCode(List<DialogSegment> segs)
    {
        if (segs.Count == 0 || segs[0].Kind != DialogSegmentKind.Text)
        {
            return segs;
        }

        string trimmed = segs[0].Value.TrimStart('\r', '\n', ' ', '\t');
        if (trimmed.Length == segs[0].Value.Length)
        {
            return segs;
        }

        // Replace the first segment with a trimmed copy.  DialogSegment is a
        // record so we use `with`.
        List<DialogSegment> copy = new(segs);
        copy[0] = segs[0] with { Value = trimmed };
        return copy;
    }

    /// <summary>
    /// Build the framed Border that wraps a Code-segment group.  Uses
    /// <c>AppCodeBlockBackgroundBrush</c> / <c>AppCodeBlockBorderBrush</c> via
    /// <see cref="DynamicResourceExtension"/> so the pane re-themes when the
    /// user toggles light / dark.
    /// </summary>
    private static Border BuildCodePane(List<DialogSegment> segs, Window dialogWindow, DialogAutomationIds.Sequence ids)
    {
        Border pane = new()
        {
            Child = BuildSegmentedTextBlockFor(segs, dialogWindow, ids, isCode: true),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(10, 8),
        };
        pane[!Border.BackgroundProperty] = new DynamicResourceExtension("AppCodeBlockBackgroundBrush");
        pane[!Border.BorderBrushProperty] = new DynamicResourceExtension("AppCodeBlockBorderBrush");
        return pane;
    }

    /// <summary>
    /// Append a path segment: monospace + accent text run, then a 📋 button
    /// that copies the path.  Its tooltip shows the full path, so very long
    /// paths remain inspectable without selecting and copying the run, and
    /// becomes "Copied!" once it has copied; its name stays "Copy" and the
    /// path, which says what a press does.
    /// </summary>
    private static void AppendPathRun(SelectableTextBlock tb, string path, Window dialogWindow, DialogAutomationIds.Sequence ids)
    {
        Run pathRun = new(path)
        {
            FontFamily = new FontFamily("Consolas,Menlo,monospace"),
            Foreground = new SolidColorBrush(Color.Parse("#1F6FEB")),
            FontWeight = FontWeight.SemiBold,
        };
        tb.Inlines!.Add(pathRun);

        Button copyButton = GlyphButton(
            "📋", 12, new SolidColorBrush(Color.Parse("#888888")), DialogTips.CopyPath(path), ids.Next(DialogAutomationIds.CopyPath));
        copyButton.Click += OnCopyClick;

        // Async void, as an event handler has to be, so the whole body is guarded: a throw escaping
        // it would reach the dispatcher and end the process over a failed copy.
        async void OnCopyClick(object? sender, RoutedEventArgs e)
        {
            try
            {
                TopLevel? top = TopLevel.GetTopLevel(dialogWindow);
                if (top?.Clipboard is { } clip)
                {
                    await clip.SetTextAsync(path);
                    ToolTip.SetTip(copyButton, DialogTips.Copied);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Copying {Path} to the clipboard failed", path);
            }
        }

        // BaselineAlignment.Center centres the glyph on the surrounding
        // text's vertical centre. Default `Baseline` aligns the glyph's
        // bottom to the text baseline, which leaves the emoji floating
        // above the path text — visually mis-aligned. (Smoke-reported)
        // Setting VerticalAlignment on the inner control
        // alone has no effect across the inline boundary.
        tb.Inlines.Add(new InlineUIContainer
        {
            Child = copyButton,
            BaselineAlignment = BaselineAlignment.Center,
        });
    }

    /// <summary>
    /// Append a hyperlink segment: accent + underlined label, then a small
    /// ↗ button that hands the URL to <see cref="OpenLink"/>.  The visible
    /// label is the segment's <see cref="DialogSegment.Value"/>; the URL is
    /// from <see cref="DialogSegment.Url"/>.
    /// </summary>
    private static void AppendHyperlinkRun(SelectableTextBlock tb, string label, string url, DialogAutomationIds.Sequence ids)
    {
        Run linkRun = new(label)
        {
            Foreground = new SolidColorBrush(Color.Parse("#1F6FEB")),
            TextDecorations = TextDecorations.Underline,
        };
        tb.Inlines!.Add(linkRun);

        Button openButton = GlyphButton(
            "↗", 11, new SolidColorBrush(Color.Parse("#1F6FEB")), DialogTips.OpenLink(url), ids.Next(DialogAutomationIds.OpenLink));
        openButton.Click += (_, _) =>
        {
            if (string.IsNullOrEmpty(url))
            {
                return;
            }

            OpenLink(url);
        };

        tb.Inlines.Add(new InlineUIContainer { Child = openButton });
    }

    /// <summary>
    /// A 📋 or ↗ button, sized and coloured as the text glyph it replaced, named, and with a tooltip that
    /// says what a press does.
    /// </summary>
    private static Button GlyphButton(string glyph, double fontSize, IBrush foreground, string name, string automationId)
    {
        Button button = new()
        {
            Content = glyph,
            FontSize = fontSize,
            // A theme's button may draw its label heavier; the glyph it replaced took the text's weight.
            FontWeight = FontWeight.Normal,
            Margin = new Thickness(2, 0, 0, 0),
            Padding = new Thickness(0),
            BorderThickness = new Thickness(0),
            MinWidth = 0,
            MinHeight = 0,
            Background = Brushes.Transparent,
            Cursor = new Cursor(StandardCursorType.Hand),
            Foreground = foreground,
            VerticalAlignment = VerticalAlignment.Center,
        };
        button.Classes.Add(GlyphButtonClass);

        // The visible label is the glyph; the name says what a press does, as the tooltip does, so a
        // screen reader never reads the glyph's own name.
        AutomationProperties.SetName(button, name);
        AutomationProperties.SetAutomationId(button, automationId);
        ToolTip.SetTip(button, name);
        return button;
    }

    /// <summary>
    /// Adds to <paramref name="dialogWindow"/> the styles that keep its 📋 and ↗ buttons unfilled when they
    /// are pointed at or pressed, where a theme fills the button's <c>PART_ContentPresenter</c>.
    /// </summary>
    /// <remarks>
    /// The window's styles are applied after the application's, so these win over the theme's own setters
    /// for the same states; a theme whose button names its presenter otherwise keeps its fills.
    /// </remarks>
    private static void KeepGlyphButtonsUnfilled(Window dialogWindow)
    {
        dialogWindow.Styles.Add(UnfilledWhen(":pointerover"));
        dialogWindow.Styles.Add(UnfilledWhen(":pressed"));
    }

    private static Style UnfilledWhen(string pseudoClass)
    {
        Style style = new(selector => selector
            .OfType<Button>()
            .Class(GlyphButtonClass)
            .Class(pseudoClass)
            .Template()
            .OfType<ContentPresenter>()
            .Name("PART_ContentPresenter"));
        style.Setters.Add(new Setter(ContentPresenter.BackgroundProperty, Brushes.Transparent));
        style.Setters.Add(new Setter(ContentPresenter.BorderBrushProperty, Brushes.Transparent));
        return style;
    }

    /// <summary>
    /// Opens <paramref name="url"/> with the platform shell. A failure is left silent: the link's text can
    /// still be selected and copied.
    /// </summary>
    private static void OpenWithShell(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url)
            {
                UseShellExecute = true,
            });
        }
        catch
        {
            // Shell-open failed — leave silent. The URL is still selectable
            // in the SelectableTextBlock so the user can copy it manually.
        }
    }
}
