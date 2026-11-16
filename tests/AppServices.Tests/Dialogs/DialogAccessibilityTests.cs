using System.Reflection;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Bennewitz.Ninja.AppServices.AvaloniaUI.Dialogs;

namespace AppServices.Tests.Dialogs;

/// <summary>
/// Every interactive control in the dialogs this library builds in C# carries a non-empty,
/// clean-text <c>AutomationProperties.Name</c>, so a screen reader has something to announce, and a
/// tooltip that is not blank, so the person pointing at it is told what it does.
/// </summary>
/// <remarks>
/// <para>
/// Ported from ClaudeForge's <c>AccessibilityCoverageTests</c>, which guarded these two dialogs
/// alongside two live-log windows that stayed in ClaudeForge. There is no markup to scan, so each
/// dialog is built for real on the headless UI thread and its logical tree is walked.
/// </para>
/// <para>
/// The walk covers the logical tree of the UNSHOWN window, plus any <see cref="ContextMenu"/> hung
/// off a control, because a context menu joins the logical tree only when it opens. The window is
/// deliberately not shown: applying templates would pull Avalonia's own template parts into view,
/// and those are not this library's to name. Controls in a flyout or created inside a template are
/// out of reach; add a root here if a dialog ever grows one.
/// </para>
/// <para>
/// Each test also pins the NUMBER of interactive controls found. A new control must bump it, and a
/// count lower than expected means the walker can no longer reach something.
/// </para>
/// <para>
/// ⭐ <b>The tooltip tests are XamlQuality's <c>BNXQ1011</c> for controls built in code</b>, which
/// that rule cannot see (<c>plans/00005</c>, decision 5). They require a tooltip of every control
/// <see cref="IsInteractive"/> requires a name of: one list of what counts, this walker's, and no copy
/// of the rule's list of input types to drift from it.
/// </para>
/// </remarks>
public sealed class DialogAccessibilityTests
{
    private static HeadlessUnitTestSession Session =>
        HeadlessUnitTestSession.GetOrStartForAssembly(Assembly.GetExecutingAssembly());

    [Fact]
    public Task FatalErrorDialog_EveryInteractiveControlIsNamed()
    {
        return Session.Dispatch(() =>
        {
            FatalErrorDialog dialog = new("Title", "Message", new InvalidOperationException("boom"));

            // The details box, Copy to Clipboard, and Close.
            AssertEveryInteractiveControlIsNamed(dialog, expectedControls: 3);
        }, CancellationToken.None);
    }

    [Fact]
    public Task NonFatalNoticeDialog_EveryInteractiveControlIsNamed()
    {
        return Session.Dispatch(() =>
        {
            NonFatalNoticeDialog dialog = new("Title", "Message", "line 1\nline 2");

            // The details box, Copy to Clipboard, and Close.
            AssertEveryInteractiveControlIsNamed(dialog, expectedControls: 3);
        }, CancellationToken.None);
    }

    [Fact]
    public Task FatalErrorDialog_EveryInteractiveControlHasATooltip()
    {
        return Session.Dispatch(() =>
        {
            FatalErrorDialog dialog = new("Title", "Message", new InvalidOperationException("boom"));

            // The details box, Copy to Clipboard, and Close.
            AssertEveryInteractiveControlHasATooltip(dialog, expectedControls: 3);
        }, CancellationToken.None);
    }

    [Fact]
    public Task NonFatalNoticeDialog_EveryInteractiveControlHasATooltip()
    {
        return Session.Dispatch(() =>
        {
            NonFatalNoticeDialog dialog = new("Title", "Message", "line 1\nline 2");

            // The details box, Copy to Clipboard, and Close.
            AssertEveryInteractiveControlHasATooltip(dialog, expectedControls: 3);
        }, CancellationToken.None);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static void AssertEveryInteractiveControlIsNamed(Window window, int expectedControls)
    {
        List<Control> interactive = CollectInteractiveControls(window);
        List<string> failures = [];
        string owner = $"{window.GetType().Name} \"{window.Title}\"";

        foreach (Control control in interactive)
        {
            string? name = AutomationProperties.GetName(control);
            string label = $"{control.GetType().Name} \"{Describe(control)}\"";

            if (string.IsNullOrWhiteSpace(name))
            {
                failures.Add($"  • {label}: AutomationProperties.Name is not set.");
            }
            else if (name.Any(char.IsSurrogate))
            {
                // Emoji live outside the Basic Multilingual Plane, so a surrogate pair in a name
                // means a screen reader will read a glyph name aloud ("clipboard Copy").
                failures.Add($"  • {label}: AutomationProperties.Name \"{name}\" contains an emoji.");
            }
        }

        Assert.True(failures.Count == 0,
            $"Accessibility coverage regression in {owner}: every interactive control needs a "
            + "clean-text AutomationProperties.Name.\n\n" + string.Join('\n', failures)
            + "\n\nFix: call AutomationProperties.SetName(control, \"...\") where the control is built, "
            + "plus SetHelpText when the visible label is ambiguous.");

        Assert.True(interactive.Count == expectedControls,
            $"{owner}: the walker found {interactive.Count} interactive controls but this test expects "
            + $"{expectedControls}. If you added or removed a control, update the expected count. If the "
            + "count dropped without a removal, the walker no longer reaches a control (a flyout or "
            + "template root, for example) and needs a new root.");
    }

    /// <summary>
    /// Every control the walker finds carries a tooltip that is not blank: not unset, and not a string
    /// of white space. Content other than a string counts as a tooltip.
    /// </summary>
    /// <remarks>
    /// The count is asserted first, so a walker that found nothing cannot pass as a dialog whose every
    /// control has a tooltip.
    /// </remarks>
    private static void AssertEveryInteractiveControlHasATooltip(Window window, int expectedControls)
    {
        List<Control> interactive = CollectInteractiveControls(window);
        string owner = $"{window.GetType().Name} \"{window.Title}\"";

        Assert.True(interactive.Count == expectedControls,
            $"{owner}: the walker found {interactive.Count} interactive controls but this test expects "
            + $"{expectedControls}, so it cannot say each one has a tooltip. If you added or removed a "
            + "control, update the expected count here and in the naming test.");

        List<string> failures = [];

        foreach (Control control in interactive)
        {
            object? tip = ToolTip.GetTip(control);
            string label = $"{control.GetType().Name} \"{Describe(control)}\"";

            if (tip is null)
            {
                failures.Add($"  • {label}: ToolTip.Tip is not set.");
            }
            else if (tip is string text && string.IsNullOrWhiteSpace(text))
            {
                failures.Add($"  • {label}: ToolTip.Tip is blank.");
            }
        }

        Assert.True(failures.Count == 0,
            $"Tooltip coverage regression in {owner}: every interactive control needs a tooltip that says "
            + "what it does to the person pointing at it.\n\n" + string.Join('\n', failures)
            + "\n\nFix: call ToolTip.SetTip(control, \"...\") where the control is built; where it has help "
            + "text, the tooltip says the same.");
    }

    /// <summary>
    /// Every interactive control reachable from <paramref name="window"/> through the logical
    /// tree, descending into each control's <see cref="Control.ContextMenu"/> as an extra root.
    /// </summary>
    private static List<Control> CollectInteractiveControls(Window window)
    {
        List<Control> result = [];
        HashSet<Control> seen = [];
        Queue<ILogical> roots = new();
        roots.Enqueue(window);

        while (roots.Count > 0)
        {
            ILogical root = roots.Dequeue();
            foreach (ILogical logical in Enumerable.Repeat(root, 1).Concat(root.GetLogicalDescendants()))
            {
                if (logical is not Control control || !seen.Add(control))
                {
                    continue;
                }

                if (IsInteractive(control))
                {
                    result.Add(control);
                }

                if (control.ContextMenu is { } menu)
                {
                    roots.Enqueue(menu);

                    // Items added directly are logical children of the menu; enqueue them too so the
                    // walk does not depend on that detail of ItemsControl.
                    foreach (ILogical menuItem in menu.Items.OfType<ILogical>())
                    {
                        roots.Enqueue(menuItem);
                    }
                }
            }
        }

        return result;
    }

    private static bool IsInteractive(Control control)
    {
        // Button covers ToggleButton, CheckBox, RadioButton, ToggleSwitch and RepeatButton.
        // DataGrid is absent because this library does not reference Avalonia.Controls.DataGrid.
        return control is Button
                   or TextBox
                   or ComboBox
                   or ListBox
                   or MenuItem
                   or Slider
                   or NumericUpDown
                   or AutoCompleteBox
                   or DatePicker
                   or TimePicker
                   or CalendarDatePicker
                   or SelectableTextBlock
               || (control is TextBlock text && IsHandCursor(text.Cursor) && !IsInsideButton(text));
    }

    /// <summary>
    /// A TextBlock inside a Button takes its name from the Button and is never separately
    /// focusable, so naming it as well would only make a screen reader say it twice.
    /// </summary>
    /// <remarks>
    /// ⚠ Cursor is an INHERITED property, so a hand-cursor Button's content TextBlock would trip
    /// the hand-cursor rule below on a control that is not a link at all.
    /// </remarks>
    private static bool IsInsideButton(Control control)
    {
        for (ILogical? parent = control.GetLogicalParent(); parent is not null; parent = parent.GetLogicalParent())
        {
            if (parent is Button)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// A hand cursor on a TextBlock marks a link-styled TextBlock acting as a button, which a
    /// keyboard cannot reach. <see cref="Cursor"/> exposes no type accessor; its
    /// <see cref="Cursor.ToString"/> is the <see cref="StandardCursorType"/> name.
    /// </summary>
    private static bool IsHandCursor(Cursor? cursor)
    {
        return cursor is not null
               && string.Equals(cursor.ToString(), nameof(StandardCursorType.Hand), StringComparison.Ordinal);
    }

    private static string Describe(Control control)
    {
        string? text = control switch
        {
            TextBlock tb => tb.Text,
            ContentControl cc => cc.Content?.ToString(),
            MenuItem mi => mi.Header?.ToString(),
            TextBox tbx => tbx.Text,
            _ => control.Name,
        };

        text = (text ?? string.Empty).ReplaceLineEndings(" ");
        return text.Length > 40 ? text[..40] + "…" : text;
    }
}
