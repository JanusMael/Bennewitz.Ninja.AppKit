using System.Reflection;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Threading;
using Bennewitz.Ninja.AppServices.Abstractions.Dialogs;
using Bennewitz.Ninja.AppServices.AvaloniaUI.Dialogs;
using Semi.Avalonia;

namespace AppServices.Tests.Dialogs;

/// <summary>
/// The 📋 after a path and the ↗ after a link are buttons that keep the look of the text glyphs they
/// replaced (<c>plans/00007</c>, decision 5): a click, Enter, Space and UI Automation's invoke each make
/// the 📋 put its path on the clipboard and the ↗ hand its link to the opener, Tab reaches each, a click
/// selects no text, and under Semi each adds no border or padding around its glyph, is no taller than a
/// line of its text, is drawn at the weight of the glyph it replaced, and paints no fill when it is pointed
/// at or pressed.
/// </summary>
/// <remarks>
/// <para>
/// Each runs in every window that shows a message, from the table of windows, so each window's styles are
/// held as well as the buttons. A click is the headless platform's mouse at the glyph's centre; a key goes
/// to the shown window with the glyph focused. The opener is replaced for every test, so no browser opens,
/// and put back in <see cref="Dispose"/>.
/// </para>
/// <para>
/// Semi, ClaudeForge's theme, is added to the application of the test that measures under it, as
/// <c>plans/00007</c> asks; each dispatch builds an application of its own, so it reaches no other test.
/// </para>
/// </remarks>
public sealed class DialogGlyphButtonTests : IDisposable
{
    private readonly List<string> _opened = [];

    public DialogGlyphButtonTests()
    {
        DialogMessageContent.OpenLink = _opened.Add;
    }

    /// <summary>A way a person, or UI Automation, presses a button.</summary>
    public enum Pressing
    {
        Click,
        Enter,
        Space,
        Invoke,
    }

    /// <summary>Every window that shows a message, with every way of pressing a glyph.</summary>
    public static TheoryData<string, Pressing> WindowsAndPressings
    {
        get
        {
            TheoryData<string, Pressing> data = [];
            foreach (string window in DialogWindows.WithMessages)
            {
                foreach (Pressing pressing in Enum.GetValues<Pressing>())
                {
                    data.Add(window, pressing);
                }
            }

            return data;
        }
    }

    /// <summary>Every window that shows a message.</summary>
    public static TheoryData<string> WindowsWithMessages => new(DialogWindows.WithMessages);

    private static HeadlessUnitTestSession Session =>
        HeadlessUnitTestSession.GetOrStartForAssembly(Assembly.GetExecutingAssembly());

    public void Dispose()
    {
        DialogMessageContent.ResetForTests();
    }

    // -----------------------------------------------------------------------
    // What a press does
    // -----------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(WindowsAndPressings))]
    public Task Each_way_of_pressing_a_copy_button_puts_its_path_on_the_clipboard(string window, Pressing pressing) =>
        Session.Dispatch(() =>
        {
            Window dialog = Shown(window);
            DialogInput.ClearClipboard(dialog);

            Press(dialog, Glyph(dialog, "DialogCopyPath2"), pressing);
            Assert.Equal(DialogWindows.SecondPath, DialogInput.ClipboardText(dialog));

            Press(dialog, Glyph(dialog, "DialogCopyPath"), pressing);
            Assert.Equal(DialogWindows.FirstPath, DialogInput.ClipboardText(dialog));
        }, CancellationToken.None);

    [Theory]
    [MemberData(nameof(WindowsAndPressings))]
    public Task Each_way_of_pressing_an_open_button_hands_its_link_to_the_opener(string window, Pressing pressing) =>
        Session.Dispatch(() =>
        {
            Window dialog = Shown(window);

            Press(dialog, Glyph(dialog, "DialogOpenLink2"), pressing);
            Press(dialog, Glyph(dialog, "DialogOpenLink"), pressing);

            Assert.Equal(new[] { DialogWindows.SecondLink, DialogWindows.FirstLink }, _opened);
        }, CancellationToken.None);

    [Theory]
    [MemberData(nameof(WindowsWithMessages))]
    public Task A_copy_button_says_Copied_once_it_has_copied_and_its_name_still_says_what_a_press_does(string window) =>
        Session.Dispatch(() =>
        {
            Window dialog = Shown(window);
            Button copy = Glyph(dialog, "DialogCopyPath");
            Assert.Equal($"Copy {DialogWindows.FirstPath}", ToolTip.GetTip(copy));

            DialogInput.Press(copy);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal("Copied!", ToolTip.GetTip(copy));
            Assert.Equal($"Copy {DialogWindows.FirstPath}", ControlAutomationPeer.CreatePeerForElement(copy).GetName());
        }, CancellationToken.None);

    /// <summary>
    /// Two clicks on a glyph, a double click to its text block, select no text: the button takes the press,
    /// so its text block never sees it.
    /// </summary>
    [Theory]
    [MemberData(nameof(WindowsWithMessages))]
    public Task A_click_on_a_glyph_selects_no_text(string window) => Session.Dispatch(() =>
    {
        Window dialog = Shown(window);
        DialogInput.ClearClipboard(dialog);
        Button copy = Glyph(dialog, "DialogCopyPath");
        Button open = Glyph(dialog, "DialogOpenLink");
        SelectableTextBlock text = TextBlockOf(copy);

        DialogInput.Click(dialog, copy, times: 2);
        DialogInput.Click(dialog, open, times: 2);

        Assert.Same(text, TextBlockOf(open));
        Assert.Equal(string.Empty, text.SelectedText);
        Assert.Equal((0, 0), (text.SelectionStart, text.SelectionEnd));
        Assert.Equal(DialogWindows.FirstPath, DialogInput.ClipboardText(dialog));
        Assert.Equal(new[] { DialogWindows.FirstLink, DialogWindows.FirstLink }, _opened);
    }, CancellationToken.None);

    /// <summary>
    /// Where focus rests on a 📋, Enter presses it and not the dialog's default button, which a
    /// confirmation that is not destructive has (<c>plans/00007</c>, Limits).
    /// </summary>
    [Fact]
    public Task Enter_on_a_focused_copy_button_copies_and_leaves_the_confirmation_open() => Session.Dispatch(() =>
    {
        MessageConfirmDialog dialog = new(
            "Title", DialogWindows.Message(), DialogCategory.Information, "Apply", "Keep", icon: null);
        CloseCount closes = CloseCount.Of(dialog);
        dialog.Show();
        Dispatcher.UIThread.RunJobs();
        DialogInput.ClearClipboard(dialog);
        Assert.True(dialog.ConfirmButton.IsDefault);

        Press(dialog, Glyph(dialog, "DialogCopyPath"), Pressing.Enter);

        Assert.Equal(DialogWindows.FirstPath, DialogInput.ClipboardText(dialog));
        Assert.Equal(0, closes.Count);
        Assert.Null(dialog.Result);
    }, CancellationToken.None);

    // -----------------------------------------------------------------------
    // The keyboard reaches each
    // -----------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(WindowsWithMessages))]
    public Task Tab_reaches_every_glyph_button(string window) => Session.Dispatch(() =>
    {
        Window dialog = Shown(window);
        string[] glyphs = ["DialogCopyPath", "DialogCopyPath2", "DialogOpenLink", "DialogOpenLink2"];
        HashSet<string> reached = new(StringComparer.Ordinal);

        // Twice round every interactive control in the window is more than enough.
        for (int tab = 0; tab < 24; tab++)
        {
            DialogInput.PressKey(dialog, PhysicalKey.Tab);
            Dispatcher.UIThread.RunJobs();
            if (dialog.FocusManager?.GetFocusedElement() is Control focused
                && AutomationProperties.GetAutomationId(focused) is { } id)
            {
                reached.Add(id);
            }
        }

        Assert.All(glyphs, glyph => Assert.Contains(glyph, reached));
    }, CancellationToken.None);

    // -----------------------------------------------------------------------
    // Today's look, under Semi
    // -----------------------------------------------------------------------

    /// <summary>
    /// Each glyph is no taller than a line of its own text block that holds no glyph: the button adds no
    /// height of its own, where Semi gives a button a minimum height and padding.
    /// </summary>
    [Theory]
    [MemberData(nameof(WindowsWithMessages))]
    public Task Under_Semi_a_glyph_button_is_no_taller_than_a_line_of_its_text(string window) => Session.Dispatch(() =>
    {
        Application.Current!.Styles.Add(new SemiTheme());
        Window dialog = Shown(window);

        foreach (Button glyph in Glyphs(dialog))
        {
            SelectableTextBlock text = TextBlockOf(glyph);
            string content = text.Inlines!.Text!;
            TextLine plainLine = Assert.Single(text.TextLayout.TextLines.Where(line => HoldsNoGlyph(content, line)).Take(1));

            Assert.True(glyph.Bounds.Height > 0, $"{Id(glyph)} was not laid out.");
            Assert.True(glyph.Bounds.Height <= plainLine.Height,
                $"{Id(glyph)} is {glyph.Bounds.Height} high, taller than a line of its text, {plainLine.Height}.");
        }
    }, CancellationToken.None);

    /// <summary>
    /// Each glyph button is as big as its glyph, to within the pixel layout rounding adds: no border and
    /// no padding around it, where Semi gives a button a border and pads it 12 on either side, which no
    /// height would show.
    /// </summary>
    [Theory]
    [MemberData(nameof(WindowsWithMessages))]
    public Task Under_Semi_a_glyph_button_adds_no_border_or_padding_around_its_glyph(string window) =>
        Session.Dispatch(() =>
        {
            Application.Current!.Styles.Add(new SemiTheme());
            Window dialog = Shown(window);

            foreach (Button glyph in Glyphs(dialog))
            {
                Control drawn = Assert.IsAssignableFrom<Control>(glyph.Presenter?.Child);
                Assert.True(
                    Math.Abs(glyph.Bounds.Width - drawn.Bounds.Width) < 1
                    && Math.Abs(glyph.Bounds.Height - drawn.Bounds.Height) < 1,
                    $"{Id(glyph)} is {glyph.Bounds.Size} around a glyph of {drawn.Bounds.Size}.");
            }
        }, CancellationToken.None);

    /// <summary>
    /// Each glyph is drawn at the weight of the text glyph it replaced, where Semi draws a button's label
    /// heavier.
    /// </summary>
    [Theory]
    [MemberData(nameof(WindowsWithMessages))]
    public Task Under_Semi_a_glyph_is_drawn_at_the_weight_of_the_text_glyph_it_replaced(string window) =>
        Session.Dispatch(() =>
        {
            Application.Current!.Styles.Add(new SemiTheme());
            Window dialog = Shown(window);

            Assert.All(Glyphs(dialog), glyph => Assert.Equal(FontWeight.Normal, glyph.Presenter?.FontWeight));
        }, CancellationToken.None);

    /// <summary>
    /// Pointed at and pressed through the headless platform's mouse, and pressed with Space while the
    /// pointer is elsewhere, each glyph's <c>PART_ContentPresenter</c> paints nothing, where Semi fills a
    /// button's in each state.
    /// </summary>
    /// <remarks>
    /// A press by the mouse is pointed at as well, so only Space shows the pressed state alone. Semi draws
    /// keyboard focus with a focus adorner, not on the presenter, so the ring the plan keeps is not read
    /// here.
    /// </remarks>
    [Theory]
    [MemberData(nameof(WindowsWithMessages))]
    public Task Under_Semi_a_glyph_button_pointed_at_or_pressed_paints_no_fill(string window) => Session.Dispatch(() =>
    {
        Application.Current!.Styles.Add(new SemiTheme());
        Window dialog = Shown(window);

        foreach (Button glyph in Glyphs(dialog))
        {
            Point centre = DialogInput.Centre(dialog, glyph);

            dialog.MouseMove(centre, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            Assert.True(glyph.IsPointerOver, $"{Id(glyph)} is not pointed at.");
            AssertUnfilled(glyph, "pointed at");

            dialog.MouseDown(centre, MouseButton.Left, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            Assert.True(glyph.IsPressed, $"{Id(glyph)} is not pressed.");
            AssertUnfilled(glyph, "pressed");

            dialog.MouseUp(centre, MouseButton.Left, RawInputModifiers.None);
            dialog.MouseMove(new Point(0, 0), RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            Assert.False(glyph.IsPointerOver, $"{Id(glyph)} is still pointed at.");

            Assert.True(glyph.Focus(NavigationMethod.Tab), $"{Id(glyph)} took no focus.");
            dialog.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            Assert.True(glyph.IsPressed, $"{Id(glyph)} is not pressed by Space.");
            AssertUnfilled(glyph, "pressed by Space");

            dialog.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
        }
    }, CancellationToken.None);

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static Window Shown(string window)
    {
        Window dialog = DialogWindows.Named(window).Build();
        dialog.Show();
        Dispatcher.UIThread.RunJobs();
        return dialog;
    }

    private static Button Glyph(Window dialog, string id)
    {
        return Glyphs(dialog).Single(button => Id(button) == id);
    }

    private static IEnumerable<Button> Glyphs(Window dialog)
    {
        return dialog.GetLogicalDescendants()
                     .OfType<Button>()
                     .Where(button => Id(button) is { } id && (id.StartsWith("DialogCopyPath", StringComparison.Ordinal)
                                                               || id.StartsWith("DialogOpenLink", StringComparison.Ordinal)));
    }

    private static string? Id(Control control)
    {
        return AutomationProperties.GetAutomationId(control);
    }

    /// <summary>
    /// Whether <paramref name="line"/> holds no inline control: no U+FFFC, which stands for one in its text
    /// block's text at the position the layout gives it.
    /// </summary>
    private static bool HoldsNoGlyph(string content, TextLine line)
    {
        int start = Math.Min(line.FirstTextSourceIndex, content.Length);
        int end = Math.Min(line.FirstTextSourceIndex + line.Length, content.Length);
        return end > start && content.AsSpan(start, end - start).IndexOf('￼') < 0;
    }

    /// <summary>The text block whose line holds <paramref name="glyph"/>.</summary>
    private static SelectableTextBlock TextBlockOf(Button glyph)
    {
        InlineUIContainer container = Assert.IsType<InlineUIContainer>(glyph.GetLogicalParent());
        return Assert.IsType<SelectableTextBlock>(container.GetLogicalParent());
    }

    private static void Press(Window dialog, Button glyph, Pressing pressing)
    {
        switch (pressing)
        {
            case Pressing.Click:
                DialogInput.Click(dialog, glyph);
                break;
            case Pressing.Enter:
                Assert.True(glyph.Focus(NavigationMethod.Tab), $"{Id(glyph)} took no focus.");
                DialogInput.PressKey(dialog, PhysicalKey.Enter);
                break;
            case Pressing.Space:
                Assert.True(glyph.Focus(NavigationMethod.Tab), $"{Id(glyph)} took no focus.");
                DialogInput.PressKey(dialog, PhysicalKey.Space);
                break;
            case Pressing.Invoke:
                DialogInput.Press(glyph);
                break;
        }

        Dispatcher.UIThread.RunJobs();
    }

    private static void AssertUnfilled(Button glyph, string state)
    {
        IBrush? background = glyph.Presenter?.Background;
        IBrush? border = glyph.Presenter?.BorderBrush;
        Assert.True(Paints(background) is false,
            $"{Id(glyph)} {state} fills its presenter with {background}, where its text glyph painted nothing.");
        Assert.True(Paints(border) is false,
            $"{Id(glyph)} {state} draws its presenter's border with {border}, where its text glyph drew nothing.");
    }

    /// <summary>Whether <paramref name="brush"/> puts any colour on the screen.</summary>
    private static bool Paints(IBrush? brush)
    {
        return brush switch
        {
            null => false,
            ISolidColorBrush solid => solid.Color.A > 0 && solid.Opacity > 0,
            _ => true,
        };
    }
}
