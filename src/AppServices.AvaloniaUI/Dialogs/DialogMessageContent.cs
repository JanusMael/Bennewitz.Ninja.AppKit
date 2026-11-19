using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Bennewitz.Ninja.AppServices.Abstractions.Dialogs;
using Serilog;

namespace Bennewitz.Ninja.AppServices.AvaloniaUI.Dialogs;

/// <summary>
/// Renders a <see cref="DialogMessage"/> into the controls a dialog shows it in: text, bold text, a path
/// with its 📋, a link with its ↗, and code in a framed pane.
/// </summary>
/// <remarks>
/// Moved out of <see cref="AvaloniaDialogService"/> with the dialogs that show a message
/// (<c>plans/00007</c>, decision 1); what it builds is unchanged.
/// </remarks>
internal static class DialogMessageContent
{
    /// <summary>
    /// Render a <see cref="DialogMessage"/>'s segments into a
    /// <see cref="SelectableTextBlock"/>'s inline collection.  Path segments
    /// are rendered monospace + accent and followed by an inline 📋 glyph
    /// that copies the path to the clipboard on click; hyperlink segments
    /// render accent + underline and are followed by a ↗ glyph that opens
    /// the URL in the user's default browser.
    /// </summary>
    internal static SelectableTextBlock BuildSegmentedTextBlock(DialogMessage message, Window dialogWindow)
    {
        return BuildSegmentedTextBlockFor(message.Segments, dialogWindow);
    }

    /// <summary>
    /// Render an arbitrary <see cref="DialogSegment"/> sequence into a
    /// SelectableTextBlock.  Extracted from <see cref="BuildSegmentedTextBlock"/>
    /// so the segment-grouping helper (<see cref="BuildSegmentedDialogContent"/>)
    /// can call it per-group without having to construct a synthetic
    /// <see cref="DialogMessage"/>.
    /// </summary>
    private static SelectableTextBlock BuildSegmentedTextBlockFor(
        IEnumerable<DialogSegment> segments, Window dialogWindow)
    {
        SelectableTextBlock tb = new()
        {
            TextWrapping = TextWrapping.Wrap,
        };

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
                    AppendPathRun(tb, seg.Value, dialogWindow);
                    break;

                case DialogSegmentKind.Hyperlink:
                    AppendHyperlinkRun(tb, seg.Value, seg.Url ?? string.Empty);
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
        // Fast-path: no Code segments → identical to the legacy inline renderer.
        // Keeps every other dialog visually unchanged.
        if (!message.Segments.Any(s => s.Kind == DialogSegmentKind.Code))
        {
            return BuildSegmentedTextBlockFor(message.Segments, dialogWindow);
        }

        List<(bool IsCode, List<DialogSegment> Segments)> groups = GroupSegmentsByKind(message.Segments);
        StackPanel stack = new() { Spacing = 12 };
        for (int i = 0; i < groups.Count; i++)
        {
            (bool isCode, List<DialogSegment> segs) = groups[i];
            if (isCode)
            {
                stack.Children.Add(BuildCodePane(segs, dialogWindow));
            }
            else
            {
                bool precededByCode = i > 0 && groups[i - 1].IsCode;
                List<DialogSegment> effectiveSegs = precededByCode ? TrimLeadingWhitespaceAfterCode(segs) : segs;
                stack.Children.Add(BuildSegmentedTextBlockFor(effectiveSegs, dialogWindow));
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
    private static Border BuildCodePane(List<DialogSegment> segs, Window dialogWindow)
    {
        Border pane = new()
        {
            Child = BuildSegmentedTextBlockFor(segs, dialogWindow),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(10, 8),
        };
        pane[!Border.BackgroundProperty] = new DynamicResourceExtension("AppCodeBlockBackgroundBrush");
        pane[!Border.BorderBrushProperty] = new DynamicResourceExtension("AppCodeBlockBorderBrush");
        return pane;
    }

    /// <summary>
    /// Append a path segment: monospace + accent text run, then an inline
    /// clipboard glyph that copies the path on click.  The glyph carries a
    /// tooltip showing the full path so very long paths remain inspectable
    /// without selecting and copying the run.
    /// </summary>
    private static void AppendPathRun(SelectableTextBlock tb, string path, Window dialogWindow)
    {
        Run pathRun = new(path)
        {
            FontFamily = new FontFamily("Consolas,Menlo,monospace"),
            Foreground = new SolidColorBrush(Color.Parse("#1F6FEB")),
            FontWeight = FontWeight.SemiBold,
        };
        tb.Inlines!.Add(pathRun);

        TextBlock copyGlyph = new()
        {
            Text = "📋",
            FontSize = 12,
            Margin = new Thickness(2, 0, 0, 0),
            Cursor = new Cursor(StandardCursorType.Hand),
            Foreground = new SolidColorBrush(Color.Parse("#888888")),
            VerticalAlignment = VerticalAlignment.Center,
        };
        ToolTip.SetTip(copyGlyph, $"Copy {path}");

        copyGlyph.PointerPressed += OnCopyGlyphPressed;

        // Async void, as an event handler has to be, so the whole body is guarded: a throw escaping
        // it would reach the dispatcher and end the process over a failed copy.
        async void OnCopyGlyphPressed(object? sender, PointerPressedEventArgs e)
        {
            try
            {
                TopLevel? top = TopLevel.GetTopLevel(dialogWindow);
                if (top?.Clipboard is { } clip)
                {
                    await clip.SetTextAsync(path);
                    ToolTip.SetTip(copyGlyph, "Copied!");
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
        // Setting VerticalAlignment on the inner TextBlock
        // alone has no effect across the inline boundary.
        tb.Inlines.Add(new InlineUIContainer
        {
            Child = copyGlyph,
            BaselineAlignment = BaselineAlignment.Center,
        });
    }

    /// <summary>
    /// Append a hyperlink segment: accent + underlined label, then a small
    /// ↗ glyph that opens the URL in the user's default browser via the
    /// platform shell.  The visible label is the segment's <see cref="DialogSegment.Value"/>;
    /// the URL is from <see cref="DialogSegment.Url"/>.
    /// </summary>
    private static void AppendHyperlinkRun(SelectableTextBlock tb, string label, string url)
    {
        Run linkRun = new(label)
        {
            Foreground = new SolidColorBrush(Color.Parse("#1F6FEB")),
            TextDecorations = TextDecorations.Underline,
        };
        tb.Inlines!.Add(linkRun);

        TextBlock openGlyph = new()
        {
            Text = "↗",
            FontSize = 11,
            Margin = new Thickness(2, 0, 0, 0),
            Cursor = new Cursor(StandardCursorType.Hand),
            Foreground = new SolidColorBrush(Color.Parse("#1F6FEB")),
            VerticalAlignment = VerticalAlignment.Center,
        };
        ToolTip.SetTip(openGlyph, $"Open {url}");

        openGlyph.PointerPressed += (_, _) =>
        {
            if (string.IsNullOrEmpty(url))
            {
                return;
            }

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
        };

        tb.Inlines.Add(new InlineUIContainer { Child = openGlyph });
    }
}
