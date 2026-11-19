using System.Globalization;
using System.Reflection;
using System.Text;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Bennewitz.Ninja.AppServices.AvaloniaUI.Dialogs;

namespace AppServices.Tests.Dialogs;

/// <summary>
/// Every interactive control in every window the Avalonia package builds has a name a screen reader reads,
/// a tooltip that is not blank, and an automation id unique in its window (<c>plans/00007</c>, decision 4):
/// the six dialogs <c>AvaloniaDialogService</c> shows, and <see cref="FatalErrorDialog"/> and
/// <see cref="NonFatalNoticeDialog"/>.
/// </summary>
/// <remarks>
/// <para>
/// Ported from ClaudeForge's <c>AccessibilityCoverageTests</c>, which guarded the two public dialogs. There
/// is no markup to scan, so each window in <see cref="DialogWindows"/> is built for real on the headless UI
/// thread and its logical tree is walked; a window class the package declares that is missing from that
/// table fails.
/// </para>
/// <para>
/// The walk covers the logical tree of the UNSHOWN window, plus any <see cref="ContextMenu"/> hung off a
/// control, because a context menu joins the logical tree only when it opens. The window is deliberately
/// not shown: applying templates would pull Avalonia's own template parts into view, and those are not
/// this library's to name. A 📋 or ↗ button needs no root of its own: an <c>InlineUIContainer</c>'s child
/// is in its text block's logical tree, shown or not (measured on 2026-11-19). Controls in a flyout or
/// created inside a template are out of reach; add a root here if a window ever grows one.
/// </para>
/// <para>
/// A name is the one the control's automation peer reports, which is what UI Automation reads. A text
/// block's peer reports its text and ignores an automation name, and holds U+FFFC wherever an inline
/// control sits, itself a symbol; so the rule that a name holds no symbol, no character of category
/// <see cref="UnicodeCategory.OtherSymbol"/> and none in the Arrows block, holds every name but that of
/// a <see cref="SelectableTextBlock"/>, which shows a message's text. A text block with a hand cursor
/// stands in for a button, and its text is a name the package gives. The words a caller passes here hold
/// none.
/// </para>
/// <para>
/// Each test asserts the number of interactive controls first, from the window's row in the table. A new
/// control must add its id there, and a count lower than expected means the walker can no longer reach
/// something.
/// </para>
/// <para>
/// ⭐ <b>The tooltip test is XamlQuality's <c>BNXQ1011</c> for controls built in code</b>, which that
/// rule cannot see (<c>plans/00005</c>, decision 5). It requires a tooltip of every control
/// <see cref="IsInteractive"/> requires a name of: one list of what counts, this walker's, and no copy of
/// the rule's list of input types to drift from it.
/// </para>
/// </remarks>
public sealed class DialogAccessibilityTests
{
    private static HeadlessUnitTestSession Session =>
        HeadlessUnitTestSession.GetOrStartForAssembly(Assembly.GetExecutingAssembly());

    /// <summary>The name of every window class in the table.</summary>
    public static TheoryData<string> Windows => new(DialogWindows.All.Select(entry => entry.Type.Name));

    [Fact]
    public void Every_window_class_the_package_declares_is_in_the_table()
    {
        string[] declared = typeof(FatalErrorDialog).Assembly.GetTypes()
                                                    .Where(type => typeof(Window).IsAssignableFrom(type) && !type.IsAbstract)
                                                    .Select(type => type.FullName!)
                                                    .Order(StringComparer.Ordinal)
                                                    .ToArray();
        string[] tabled = DialogWindows.All.Select(entry => entry.Type.FullName!).Order(StringComparer.Ordinal).ToArray();

        Assert.NotEmpty(declared);
        Assert.True(declared.SequenceEqual(tabled),
            "The table of windows in DialogWindows must hold every window class the Avalonia package declares, "
            + "each with the arguments that build it.\n\nDeclared:\n  " + string.Join("\n  ", declared)
            + "\n\nIn the table:\n  " + string.Join("\n  ", tabled));
    }

    [Theory]
    [MemberData(nameof(Windows))]
    public Task Every_interactive_control_is_named_by_its_peer(string window) => Session.Dispatch(() =>
    {
        (Window built, List<Control> interactive) = Walk(window);
        List<string> failures = [];

        foreach (Control control in interactive)
        {
            string name = ControlAutomationPeer.CreatePeerForElement(control).GetName();

            if (string.IsNullOrWhiteSpace(name))
            {
                failures.Add($"  • {Label(control)}: its automation peer reports no name.");
            }
            else if (control is not SelectableTextBlock && FirstSymbol(name) is { } symbol)
            {
                // A screen reader reads a symbol's own name aloud: "clipboard Copy", "north east arrow".
                failures.Add($"  • {Label(control)}: its name \"{name}\" holds the symbol U+{symbol.Value:X4}.");
            }
        }

        Assert.True(failures.Count == 0,
            $"Accessibility coverage regression in {Owner(built)}: every interactive control needs a name its "
            + "automation peer reports, with no symbol in a name the package gives.\n\n" + string.Join('\n', failures)
            + "\n\nFix: a button is named by its label, or by AutomationProperties.SetName where its label is a "
            + "glyph; a text box by AutomationProperties.SetName.");
    }, CancellationToken.None);

    /// <summary>
    /// Every control the walker finds carries a tooltip that is not blank: not unset, and not a string of
    /// white space. Content other than a string counts as a tooltip.
    /// </summary>
    [Theory]
    [MemberData(nameof(Windows))]
    public Task Every_interactive_control_has_a_tooltip(string window) => Session.Dispatch(() =>
    {
        (Window built, List<Control> interactive) = Walk(window);
        List<string> failures = [];

        foreach (Control control in interactive)
        {
            object? tip = ToolTip.GetTip(control);

            if (tip is null)
            {
                failures.Add($"  • {Label(control)}: ToolTip.Tip is not set.");
            }
            else if (tip is string text && string.IsNullOrWhiteSpace(text))
            {
                failures.Add($"  • {Label(control)}: ToolTip.Tip is blank.");
            }
        }

        Assert.True(failures.Count == 0,
            $"Tooltip coverage regression in {Owner(built)}: every interactive control needs a tooltip that says "
            + "what it does to the person pointing at it.\n\n" + string.Join('\n', failures)
            + "\n\nFix: call ToolTip.SetTip(control, \"...\") where the control is built; where it has help "
            + "text, the tooltip says the same.");
    }, CancellationToken.None);

    /// <summary>
    /// Every control the walker finds carries an automation id, no two alike in the window, and the ids are
    /// the ones the window's row in the table names: <c>Dialog</c> and each control's role, a role the window
    /// repeats numbered from its second.
    /// </summary>
    [Theory]
    [MemberData(nameof(Windows))]
    public Task Every_interactive_control_has_an_automation_id_unique_in_its_window(string window) => Session.Dispatch(() =>
    {
        (Window built, List<Control> interactive) = Walk(window);
        List<string> failures = [];
        Dictionary<string, Control> seen = new(StringComparer.Ordinal);

        foreach (Control control in interactive)
        {
            string? id = AutomationProperties.GetAutomationId(control);

            if (string.IsNullOrWhiteSpace(id))
            {
                failures.Add($"  • {Label(control)}: AutomationProperties.AutomationId is not set.");
            }
            else if (!seen.TryAdd(id, control))
            {
                failures.Add($"  • {Label(control)}: its id \"{id}\" is also {Label(seen[id])}'s.");
            }
        }

        Assert.True(failures.Count == 0,
            $"Automation id regression in {Owner(built)}: every interactive control needs an id no other control "
            + "in its window has, so a harness finds it whatever its label.\n\n" + string.Join('\n', failures)
            + "\n\nFix: call AutomationProperties.SetAutomationId(control, DialogAutomationIds.<Role>) where the "
            + "control is built, numbering a role the window repeats.");

        Assert.Equal(
            DialogWindows.Named(window).Ids.Order(StringComparer.Ordinal),
            seen.Keys.Order(StringComparer.Ordinal));
    }, CancellationToken.None);

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Builds <paramref name="window"/> as its row in the table says, walks it, and asserts the walker found
    /// as many interactive controls as the row has ids.
    /// </summary>
    private static (Window Built, List<Control> Interactive) Walk(string window)
    {
        DialogWindows.Entry entry = DialogWindows.Named(window);
        Window built = entry.Build();
        List<Control> interactive = CollectInteractiveControls(built);

        Assert.True(interactive.Count == entry.Ids.Length,
            $"{Owner(built)}: the walker found {interactive.Count} interactive controls but the table of windows "
            + $"names {entry.Ids.Length} ids for it. If you added or removed a control, update its ids in "
            + "DialogWindows. If the count dropped without a removal, the walker no longer reaches a control (a "
            + "flyout or template root, for example) and needs a new root.");

        return (built, interactive);
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

    /// <summary>
    /// The first character of <paramref name="name"/> a screen reader would read by its own name: one of
    /// category <see cref="UnicodeCategory.OtherSymbol"/>, ⚠, ⛔ and 📋 among them, or one in the Arrows
    /// block, U+2190 to U+21FF, ↗ among them, which is a mathematical symbol by category.
    /// </summary>
    private static Rune? FirstSymbol(string name)
    {
        foreach (Rune rune in name.EnumerateRunes())
        {
            if (Rune.GetUnicodeCategory(rune) == UnicodeCategory.OtherSymbol || rune.Value is >= 0x2190 and <= 0x21FF)
            {
                return rune;
            }
        }

        return null;
    }

    private static string Owner(Window window)
    {
        return $"{window.GetType().Name} \"{window.Title}\"";
    }

    private static string Label(Control control)
    {
        string? text = control switch
        {
            TextBlock tb => tb.Inlines?.Text ?? tb.Text,
            ContentControl cc => cc.Content?.ToString(),
            MenuItem mi => mi.Header?.ToString(),
            TextBox tbx => tbx.Text,
            _ => control.Name,
        };

        text = (text ?? string.Empty).ReplaceLineEndings(" ");
        text = text.Length > 40 ? text[..40] + "…" : text;
        return $"{control.GetType().Name} \"{text}\"";
    }
}
