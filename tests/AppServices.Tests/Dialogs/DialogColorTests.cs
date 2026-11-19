using System.Reflection;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Styling;
using Avalonia.Threading;
using Bennewitz.Ninja.AppServices.AvaloniaUI.Dialogs;
using Semi.Avalonia;

namespace AppServices.Tests.Dialogs;

/// <summary>
/// The dialogs' colours (<c>plans/00007</c>, decisions 6, 7 and 9): the ten <c>AppServices.*</c> keys, each
/// declared for Light, Dark and Default with the plan's value and reading against its stand-in window; every
/// window taking each colour from the package's dictionary and following a change of theme while it is open;
/// a destructive button keeping its fill and its white label pointed at and pressed; a key the host declares
/// winning; and one dictionary merged into each application, first among its merged dictionaries, by the first
/// window that reads a key.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Colours"/> lists where each window in the table of windows shows a colour, and
/// <see cref="Every_window_in_the_table_of_windows_has_its_colours_listed"/> holds it to that table, so a
/// window added there is listed here too, with no colour if it reads no key. A window listed with colours it
/// does not show fails on their count, and one listed with none that reads a key fails
/// <see cref="A_window_that_reads_no_key_merges_nothing"/>.
/// </para>
/// <para>
/// The package gives a destructive button's four keys one value in both themes, so only a host's values,
/// which differ, can show that those colours follow a change of theme.
/// </para>
/// <para>
/// Each dispatch builds an application of its own, so the theme, the styles and the resources a test gives
/// its application reach no other test.
/// </para>
/// </remarks>
public sealed class DialogColorTests
{
    /// <summary>The contrast text needs against its window.</summary>
    private const double TextContrast = 4.5;

    /// <summary>The contrast a glyph needs against its window.</summary>
    private const double GlyphContrast = 3.0;

    /// <summary>The stand-in for a light window.</summary>
    private static readonly Color LightWindow = Color.Parse("#FFFFFF");

    /// <summary>The stand-in for a dark window.</summary>
    private static readonly Color DarkWindow = Color.Parse("#1F1F1F");

    /// <summary>Each key, with its colour in Light and in Dark, as <c>plans/00007</c>'s table gives them.</summary>
    private static readonly (string Key, string Light, string Dark)[] Declared =
    [
        ("AppServices.ErrorBrush", "#C62828", "#F85149"),
        ("AppServices.WarningBrush", "#C77700", "#D29922"),
        ("AppServices.LinkBrush", "#1F6FEB", "#58A6FF"),
        ("AppServices.SubtleBrush", "#888888", "#8B949E"),
        ("AppServices.DestructiveBackgroundBrush", "#C62828", "#C62828"),
        ("AppServices.DestructivePointerOverBackgroundBrush", "#B71C1C", "#B71C1C"),
        ("AppServices.DestructivePressedBackgroundBrush", "#8E1B1B", "#8E1B1B"),
        ("AppServices.DestructiveForegroundBrush", "#FFFFFF", "#FFFFFF"),
        ("AppServices.CodeBlockBackgroundBrush", "#F4F4F4", "#1F1F1F"),
        ("AppServices.CodeBlockBorderBrush", "#D0D0D0", "#3A3A3A"),
    ];

    /// <summary>The windows with a destructive button, and the button's automation id.</summary>
    private static readonly IReadOnlyDictionary<string, string> DestructiveButtons = new Dictionary<string, string>
    {
        [nameof(MessageConfirmDialog)] = "DialogConfirm",
        [nameof(UnsavedChangesDialog)] = "DialogDontSave",
    };

    /// <summary>Where each window in the table of windows shows a colour: nowhere, where it reads no key.</summary>
    private static readonly IReadOnlyDictionary<string, Painted[]> Colours = new Dictionary<string, Painted[]>
    {
        [nameof(AlertDialog)] = [HeaderGlyph("⚠", "AppServices.ErrorBrush")],
        [nameof(MessageAlertDialog)] = [HeaderGlyph("⛔", "AppServices.ErrorBrush"), .. MessageColours(codePane: true)],
        [nameof(ConfirmDialog)] = [],
        [nameof(MessageConfirmDialog)] =
        [
            HeaderGlyph("⚠", "AppServices.WarningBrush"),
            .. DestructiveButton("DialogConfirm"),
            .. MessageColours(codePane: true),
        ],
        [nameof(InputDialog)] = [],
        [nameof(UnsavedChangesDialog)] = [.. DestructiveButton("DialogDontSave"), .. MessageColours(codePane: false)],
        [nameof(FatalErrorDialog)] = [],
        [nameof(NonFatalNoticeDialog)] = [],
    };

    /// <summary>Where a host declares its own colours.</summary>
    public enum Placement
    {
        /// <summary>Among its application's own resources.</summary>
        OwnEntry,

        /// <summary>In a dictionary it merges before the first dialog merges the package's.</summary>
        MergedBefore,

        /// <summary>In a dictionary it merges once a dialog is open.</summary>
        MergedAfter,
    }

    /// <summary>Every key the plan declares.</summary>
    public static TheoryData<string> Keys => new(Declared.Select(declared => declared.Key));

    /// <summary>Each key whose colour is read against a window, with the contrast it needs there.</summary>
    public static TheoryData<string, double> MeasuredKeys => new()
    {
        { "AppServices.ErrorBrush", GlyphContrast },
        { "AppServices.WarningBrush", GlyphContrast },
        { "AppServices.LinkBrush", TextContrast },
        { "AppServices.SubtleBrush", GlyphContrast },
    };

    /// <summary>Every window that shows a colour.</summary>
    public static TheoryData<string> WindowsThatReadAKey => new(ReadingAKey());

    /// <summary>Every window that shows no colour.</summary>
    public static TheoryData<string> WindowsThatReadNoKey =>
        new(Colours.Where(window => window.Value.Length == 0).Select(window => window.Key));

    /// <summary>Every window with a destructive button.</summary>
    public static TheoryData<string> WindowsWithADestructiveButton => new(DestructiveButtons.Keys);

    /// <summary>Every window with a destructive button, in Light and in Dark.</summary>
    public static TheoryData<string, string> WindowsWithADestructiveButtonInEachTheme
    {
        get
        {
            TheoryData<string, string> data = [];
            foreach (string window in DestructiveButtons.Keys)
            {
                data.Add(window, "Light");
                data.Add(window, "Dark");
            }

            return data;
        }
    }

    /// <summary>Every window that shows a colour, with each place a host declares its own.</summary>
    public static TheoryData<string, Placement> WindowsThatReadAKeyAndEachPlacement
    {
        get
        {
            TheoryData<string, Placement> data = [];
            foreach (string window in ReadingAKey())
            {
                foreach (Placement placement in Enum.GetValues<Placement>())
                {
                    data.Add(window, placement);
                }
            }

            return data;
        }
    }

    private static HeadlessUnitTestSession Session =>
        HeadlessUnitTestSession.GetOrStartForAssembly(Assembly.GetExecutingAssembly());

    // -----------------------------------------------------------------------
    // The keys
    // -----------------------------------------------------------------------

    [Fact]
    public Task The_dictionary_declares_every_key_for_Light_Dark_and_Default_and_nothing_else() => Session.Dispatch(() =>
    {
        DialogColors colors = new();

        Assert.Empty(colors.Keys);
        Assert.Equal(
            new[] { "Dark", "Default", "Light" },
            colors.ThemeDictionaries.Keys.Select(variant => variant.Key.ToString()).Order(StringComparer.Ordinal));
        foreach (ThemeVariant variant in new[] { ThemeVariant.Light, ThemeVariant.Dark, ThemeVariant.Default })
        {
            Assert.Equal(
                Declared.Select(declared => declared.Key).Order(StringComparer.Ordinal),
                Theme(colors, variant).Keys.Cast<string>().Order(StringComparer.Ordinal));
        }
    }, CancellationToken.None);

    [Theory]
    [MemberData(nameof(Keys))]
    public Task Each_key_takes_the_plan_s_value_in_Light_and_Dark_and_the_Light_one_in_Default(string key) =>
        Session.Dispatch(() =>
        {
            (string _, string light, string dark) = Declared.Single(declared => declared.Key == key);
            DialogColors colors = new();

            Assert.Equal(Color.Parse(light), ColorIn(colors, ThemeVariant.Light, key));
            Assert.Equal(Color.Parse(dark), ColorIn(colors, ThemeVariant.Dark, key));
            Assert.Equal(Color.Parse(light), ColorIn(colors, ThemeVariant.Default, key));
        }, CancellationToken.None);

    [Theory]
    [MemberData(nameof(MeasuredKeys))]
    public Task Each_colour_reads_against_its_stand_in_window_at_its_threshold(string key, double needed) =>
        Session.Dispatch(() =>
        {
            DialogColors colors = new();

            AssertReads(ColorIn(colors, ThemeVariant.Light, key), LightWindow, needed, $"{key} in Light");
            AssertReads(ColorIn(colors, ThemeVariant.Dark, key), DarkWindow, needed, $"{key} in Dark");
        }, CancellationToken.None);

    [Fact]
    public Task The_destructive_label_reads_as_text_on_each_of_its_fills_in_both_themes() => Session.Dispatch(() =>
    {
        DialogColors colors = new();
        string[] fills =
        [
            "AppServices.DestructiveBackgroundBrush",
            "AppServices.DestructivePointerOverBackgroundBrush",
            "AppServices.DestructivePressedBackgroundBrush",
        ];

        foreach (ThemeVariant variant in new[] { ThemeVariant.Light, ThemeVariant.Dark })
        {
            Color label = ColorIn(colors, variant, "AppServices.DestructiveForegroundBrush");
            foreach (string fill in fills)
            {
                AssertReads(label, ColorIn(colors, variant, fill), TextContrast, $"The destructive label on {fill} in {variant}");
            }
        }
    }, CancellationToken.None);

    // -----------------------------------------------------------------------
    // The windows
    // -----------------------------------------------------------------------

    [Fact]
    public void Every_window_in_the_table_of_windows_has_its_colours_listed()
    {
        Assert.Equal(
            DialogWindows.All.Select(entry => entry.Type.Name).Order(StringComparer.Ordinal),
            Colours.Keys.Order(StringComparer.Ordinal));
    }

    [Theory]
    [MemberData(nameof(WindowsThatReadAKey))]
    public Task Each_window_takes_each_colour_from_the_package_s_dictionary_and_follows_a_change_of_theme_while_open(
        string window) => Session.Dispatch(() =>
    {
        Application application = Application.Current!;
        application.RequestedThemeVariant = ThemeVariant.Light;
        Window dialog = Shown(window);
        DialogColors colors = PackageDictionary(application);

        FollowTheTheme(application, dialog, variant =>
            AssertPainted(dialog, window, key => ColorIn(colors, variant, key), $"in {variant}"));
    }, CancellationToken.None);

    /// <summary>
    /// Under a theme variant that inherits neither Light nor Dark, each window takes the Default dictionary's
    /// colours, which are the Light ones.
    /// </summary>
    [Theory]
    [MemberData(nameof(WindowsThatReadAKey))]
    public Task Under_a_theme_that_inherits_neither_Light_nor_Dark_each_window_takes_the_Light_colours(string window) =>
        Session.Dispatch(() =>
        {
            Application application = Application.Current!;
            ThemeVariant other = new("Other", null);
            application.RequestedThemeVariant = other;
            Window dialog = Shown(window);
            DialogColors colors = PackageDictionary(application);

            Assert.Equal(other, dialog.ActualThemeVariant);
            AssertPainted(dialog, window, key => ColorIn(colors, ThemeVariant.Light, key), "in a theme of its own");
        }, CancellationToken.None);

    /// <summary>
    /// With a host's colours, which differ between the themes for every key, each colour a window shows follows
    /// a change of theme while it is open, and a destructive button's do in each of its states.
    /// </summary>
    [Theory]
    [MemberData(nameof(WindowsThatReadAKey))]
    public Task Every_colour_follows_a_change_of_theme_while_open_a_destructive_button_s_in_each_of_its_states(
        string window) => Session.Dispatch(() =>
    {
        Application application = Application.Current!;
        application.Resources.ThemeDictionaries[ThemeVariant.Light] = HostColours(ThemeVariant.Light);
        application.Resources.ThemeDictionaries[ThemeVariant.Dark] = HostColours(ThemeVariant.Dark);
        application.RequestedThemeVariant = ThemeVariant.Light;
        Window dialog = Shown(window);

        FollowTheTheme(application, dialog, variant =>
            AssertPainted(dialog, window, key => HostColour(variant, key), $"in {variant}"));
        if (!DestructiveButtons.ContainsKey(window))
        {
            return;
        }

        Button button = DestructiveButtonIn(dialog, window);
        Point centre = DialogInput.Centre(dialog, button);

        dialog.MouseMove(centre, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        Assert.True(button.IsPointerOver, "The destructive button is not pointed at.");
        FollowTheTheme(application, dialog, variant => AssertDrawn(
            button,
            HostColour(variant, "AppServices.DestructivePointerOverBackgroundBrush"),
            HostColour(variant, "AppServices.DestructiveForegroundBrush"),
            $"pointed at in {variant}"));

        dialog.MouseDown(centre, MouseButton.Left, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        Assert.True(button.IsPressed, "The destructive button is not pressed.");
        FollowTheTheme(application, dialog, variant => AssertDrawn(
            button,
            HostColour(variant, "AppServices.DestructivePressedBackgroundBrush"),
            HostColour(variant, "AppServices.DestructiveForegroundBrush"),
            $"pressed in {variant}"));

        LetGoOffTheButton(dialog, button);
    }, CancellationToken.None);

    /// <summary>
    /// Under Semi, which fills a button's presenter from its own keys when it is pointed at and pressed, a
    /// destructive button is filled from the package's keys at rest, pointed at, pressed by the mouse and pressed
    /// by Space, and its label stays white.
    /// </summary>
    /// <remarks>A press by the mouse is pointed at as well, so only Space shows the pressed state alone.</remarks>
    [Theory]
    [MemberData(nameof(WindowsWithADestructiveButtonInEachTheme))]
    public Task Under_Semi_a_destructive_button_is_filled_from_its_keys_in_each_state_and_its_label_stays_white(
        string window, string theme) => Session.Dispatch(() =>
    {
        Application application = Application.Current!;
        application.Styles.Add(new SemiTheme());
        ThemeVariant variant = theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        application.RequestedThemeVariant = variant;
        Window dialog = Shown(window);
        DialogColors colors = PackageDictionary(application);
        Button button = DestructiveButtonIn(dialog, window);
        Color label = ColorIn(colors, variant, "AppServices.DestructiveForegroundBrush");
        Color atRest = ColorIn(colors, variant, "AppServices.DestructiveBackgroundBrush");
        Color pressed = ColorIn(colors, variant, "AppServices.DestructivePressedBackgroundBrush");

        AssertDrawn(button, atRest, label, "at rest");

        Point centre = DialogInput.Centre(dialog, button);
        dialog.MouseMove(centre, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        Assert.True(button.IsPointerOver, "The destructive button is not pointed at.");
        AssertDrawn(button, ColorIn(colors, variant, "AppServices.DestructivePointerOverBackgroundBrush"), label, "pointed at");

        dialog.MouseDown(centre, MouseButton.Left, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        Assert.True(button.IsPressed, "The destructive button is not pressed.");
        AssertDrawn(button, pressed, label, "pressed by the mouse");

        LetGoOffTheButton(dialog, button);
        AssertDrawn(button, atRest, label, "at rest again");

        Assert.True(button.Focus(NavigationMethod.Tab), "The destructive button took no focus.");
        dialog.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        Assert.True(button.IsPressed, "The destructive button is not pressed by Space.");
        AssertDrawn(button, pressed, label, "pressed by Space");

        // Let Space go, which presses the button and closes the dialog, so no key is left down.
        dialog.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
    }, CancellationToken.None);

    /// <summary>
    /// Under a theme that paints a button's label as well as its fill when it is pointed at and pressed, as
    /// Fluent's does (its <c>Button.xaml</c>, 48 and 58), a destructive button's label stays white, and its fill
    /// comes from its keys.
    /// </summary>
    [Theory]
    [MemberData(nameof(WindowsWithADestructiveButton))]
    public Task Under_a_theme_that_paints_a_button_s_label_a_destructive_label_stays_white_pointed_at_and_pressed(
        string window) => Session.Dispatch(() =>
    {
        Application application = Application.Current!;
        application.RequestedThemeVariant = ThemeVariant.Light;
        application.Styles.Add(PaintedLikeFluent(":pointerover"));
        application.Styles.Add(PaintedLikeFluent(":pressed"));
        Window dialog = Shown(window);
        DialogColors colors = PackageDictionary(application);
        Button button = DestructiveButtonIn(dialog, window);
        Color label = ColorIn(colors, ThemeVariant.Light, "AppServices.DestructiveForegroundBrush");

        Point centre = DialogInput.Centre(dialog, button);
        dialog.MouseMove(centre, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        Assert.True(button.IsPointerOver, "The destructive button is not pointed at.");
        AssertDrawn(
            button, ColorIn(colors, ThemeVariant.Light, "AppServices.DestructivePointerOverBackgroundBrush"), label, "pointed at");

        dialog.MouseDown(centre, MouseButton.Left, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        Assert.True(button.IsPressed, "The destructive button is not pressed.");
        AssertDrawn(button, ColorIn(colors, ThemeVariant.Light, "AppServices.DestructivePressedBackgroundBrush"), label, "pressed");

        LetGoOffTheButton(dialog, button);
    }, CancellationToken.None);

    // -----------------------------------------------------------------------
    // A host's own colours
    // -----------------------------------------------------------------------

    /// <summary>
    /// A key the host declares among its application's own resources, or in a dictionary it merges before the
    /// first dialog or once one is open, is found before the package's.
    /// </summary>
    [Theory]
    [MemberData(nameof(WindowsThatReadAKeyAndEachPlacement))]
    public Task A_key_the_host_declares_wins_over_the_package_s(string window, Placement placement) =>
        Session.Dispatch(() =>
        {
            Application application = Application.Current!;
            application.RequestedThemeVariant = ThemeVariant.Light;
            ResourceDictionary host = HostColours(ThemeVariant.Light);
            if (placement == Placement.OwnEntry)
            {
                foreach (KeyValuePair<object, object?> entry in host)
                {
                    application.Resources.Add(entry.Key, entry.Value);
                }
            }
            else if (placement == Placement.MergedBefore)
            {
                application.Resources.MergedDictionaries.Add(host);
            }

            Window dialog = Shown(window);
            if (placement == Placement.MergedAfter)
            {
                application.Resources.MergedDictionaries.Add(host);
                Dispatcher.UIThread.RunJobs();
            }

            AssertPainted(dialog, window, key => HostColour(ThemeVariant.Light, key), $"with the host's colours, {placement}");
            if (placement != Placement.OwnEntry)
            {
                Assert.Equal(2, application.Resources.MergedDictionaries.Count);
                Assert.IsType<DialogColors>(application.Resources.MergedDictionaries[0]);
                Assert.Same(host, application.Resources.MergedDictionaries[1]);
            }
        }, CancellationToken.None);

    // -----------------------------------------------------------------------
    // The merge
    // -----------------------------------------------------------------------

    [Fact]
    public Task Building_each_window_that_reads_a_key_twice_merges_one_dictionary_first_among_the_merged_ones() =>
        Session.Dispatch(() =>
        {
            Application application = Application.Current!;
            ResourceDictionary host = new();
            application.Resources.MergedDictionaries.Add(host);

            foreach (string window in ReadingAKey())
            {
                _ = DialogWindows.Named(window).Build();
                _ = DialogWindows.Named(window).Build();
            }

            Assert.Equal(2, application.Resources.MergedDictionaries.Count);
            Assert.IsType<DialogColors>(application.Resources.MergedDictionaries[0]);
            Assert.Same(host, application.Resources.MergedDictionaries[1]);
        }, CancellationToken.None);

    [Fact]
    public async Task A_second_application_gets_a_dictionary_of_its_own()
    {
        (Application Application, DialogColors Colors) first = await Session.Dispatch(BuildOne, CancellationToken.None);
        (Application Application, DialogColors Colors) second = await Session.Dispatch(BuildOne, CancellationToken.None);

        Assert.False(
            ReferenceEquals(first.Application, second.Application),
            "Both dispatches ran in one application, so this test cannot tell two applications apart.");
        Assert.NotSame(first.Colors, second.Colors);
        Assert.Same(first.Application, first.Colors.Owner);
        Assert.Same(second.Application, second.Colors.Owner);

        static (Application, DialogColors) BuildOne()
        {
            _ = DialogWindows.Named(nameof(MessageAlertDialog)).Build();
            Application application = Application.Current!;
            return (application, PackageDictionary(application));
        }
    }

    [Theory]
    [MemberData(nameof(WindowsThatReadNoKey))]
    public Task A_window_that_reads_no_key_merges_nothing(string window) => Session.Dispatch(() =>
    {
        Application application = Application.Current!;
        Assert.Empty(application.Resources.MergedDictionaries);

        _ = Shown(window);

        Assert.Empty(application.Resources.MergedDictionaries);
    }, CancellationToken.None);

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static IEnumerable<string> ReadingAKey()
    {
        return Colours.Where(window => window.Value.Length > 0).Select(window => window.Key);
    }

    private static Window Shown(string window)
    {
        Window dialog = DialogWindows.Named(window).Build();
        dialog.Show();
        Dispatcher.UIThread.RunJobs();
        return dialog;
    }

    /// <summary>The package's dictionary, the one among <paramref name="application"/>'s merged dictionaries.</summary>
    private static DialogColors PackageDictionary(Application application)
    {
        return Assert.Single(application.Resources.MergedDictionaries.OfType<DialogColors>());
    }

    private static ResourceDictionary Theme(DialogColors colors, ThemeVariant variant)
    {
        return Assert.IsType<ResourceDictionary>(colors.ThemeDictionaries[variant]);
    }

    /// <summary>The colour <paramref name="colors"/> declares for <paramref name="key"/> in <paramref name="variant"/>.</summary>
    private static Color ColorIn(DialogColors colors, ThemeVariant variant, string key)
    {
        Assert.True(Theme(colors, variant).TryGetValue(key, out object? value), $"{key} is not declared for {variant}.");
        return ColorOf(value, $"{key} for {variant}");
    }

    private static Color ColorOf(object? value, string what)
    {
        if (value is ISolidColorBrush solid)
        {
            return solid.Color;
        }

        Assert.Fail($"{what} is {value?.ToString() ?? "nothing"}, not a solid colour.");
        return default;
    }

    /// <summary>
    /// Sets <paramref name="application"/>'s theme to Light, Dark and Light again, asserting after each that the
    /// open <paramref name="dialog"/> took it.
    /// </summary>
    private static void FollowTheTheme(Application application, Window dialog, Action<ThemeVariant> assert)
    {
        foreach (ThemeVariant variant in new[] { ThemeVariant.Light, ThemeVariant.Dark, ThemeVariant.Light })
        {
            application.RequestedThemeVariant = variant;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(variant, dialog.ActualThemeVariant);
            assert(variant);
        }
    }

    /// <summary>Asserts every colour <paramref name="window"/> shows is the one <paramref name="expected"/> gives its key.</summary>
    private static void AssertPainted(Window dialog, string window, Func<string, Color> expected, string when)
    {
        foreach (Painted painted in Colours[window])
        {
            IReadOnlyList<AvaloniaObject> targets = painted.Find(dialog);
            Assert.True(
                targets.Count == painted.Count,
                $"{window} shows {targets.Count} of its {painted.What}, where it has {painted.Count}.");
            foreach (AvaloniaObject target in targets)
            {
                Color actual = ColorOf(target.GetValue(painted.Property), $"{window}'s {painted.What} {when}");
                Color wanted = expected(painted.Key);
                Assert.True(
                    actual == wanted,
                    $"{window}'s {painted.What} {when} is {actual}, where {painted.Key} is {wanted}.");
            }
        }
    }

    /// <summary>Asserts the colours <paramref name="button"/>'s presenter is filled with and its label drawn in.</summary>
    private static void AssertDrawn(Button button, Color fill, Color label, string state)
    {
        ContentPresenter? presenter = button.Presenter;
        Assert.NotNull(presenter);
        Color filled = ColorOf(presenter.Background, $"The destructive button's fill {state}");
        Assert.True(filled == fill, $"The destructive button {state} is filled with {filled}, where {fill} was expected.");

        TextBlock text = Assert.IsAssignableFrom<TextBlock>(presenter.Child);
        Color drawn = ColorOf(text.Foreground, $"The destructive button's label {state}");
        Assert.True(drawn == label, $"The destructive button's label {state} is {drawn}, where {label} was expected.");
    }

    /// <summary>Asserts <paramref name="colour"/> reads against <paramref name="behind"/> at <paramref name="needed"/> or more.</summary>
    private static void AssertReads(Color colour, Color behind, double needed, string what)
    {
        double contrast = Contrast(colour, behind);
        Assert.True(contrast >= needed, $"{what}, {colour} on {behind}, reads at {contrast:0.00}:1, below {needed}:1.");
    }

    /// <summary>The contrast ratio of two colours, as WCAG 2 defines it.</summary>
    private static double Contrast(Color first, Color second)
    {
        double one = Luminance(first);
        double other = Luminance(second);
        return (Math.Max(one, other) + 0.05) / (Math.Min(one, other) + 0.05);
    }

    private static double Luminance(Color colour)
    {
        return (0.2126 * Linear(colour.R)) + (0.7152 * Linear(colour.G)) + (0.0722 * Linear(colour.B));
    }

    private static double Linear(byte channel)
    {
        double share = channel / 255.0;
        return share <= 0.04045 ? share / 12.92 : Math.Pow((share + 0.055) / 1.055, 2.4);
    }

    /// <summary>A host's colours for <paramref name="variant"/>: every key, each unlike the package's.</summary>
    private static ResourceDictionary HostColours(ThemeVariant variant)
    {
        ResourceDictionary colours = new();
        foreach ((string Key, string Light, string Dark) declared in Declared)
        {
            colours.Add(declared.Key, new ImmutableSolidColorBrush(HostColour(variant, declared.Key)));
        }

        return colours;
    }

    /// <summary>The colour a host gives <paramref name="key"/> in <paramref name="variant"/>: one for each key and theme.</summary>
    private static Color HostColour(ThemeVariant variant, string key)
    {
        int index = Array.FindIndex(Declared, declared => declared.Key == key);
        Assert.True(index >= 0, $"{key} is not one of the package's keys.");
        return variant == ThemeVariant.Dark
            ? Color.FromRgb(0x50, 0x60, (byte)(0x70 + index))
            : Color.FromRgb(0x10, 0x20, (byte)(0x30 + index));
    }

    /// <summary>The destructive button of <paramref name="window"/>, which <paramref name="dialog"/> is.</summary>
    private static Button DestructiveButtonIn(Window dialog, string window)
    {
        return Assert.Single(ButtonsWithId(dialog, DestructiveButtons[window]));
    }

    private static Button[] ButtonsWithId(Window dialog, string id)
    {
        return dialog.GetLogicalDescendants()
                     .OfType<Button>()
                     .Where(button => AutomationProperties.GetAutomationId(button) == id)
                     .ToArray();
    }

    /// <summary>
    /// Moves the pointer off <paramref name="button"/>, which is pressed, and lets go there, where a release
    /// presses nothing, so the dialog stays open.
    /// </summary>
    private static void LetGoOffTheButton(Window dialog, Button button)
    {
        Point away = new(1, 1);
        dialog.MouseMove(away, RawInputModifiers.None);
        dialog.MouseUp(away, MouseButton.Left, RawInputModifiers.None);
        dialog.MouseMove(away, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();

        Assert.False(button.IsPressed, "The destructive button is still pressed.");
        Assert.False(button.IsPointerOver, "The destructive button is still pointed at.");
        Assert.True(dialog.IsVisible, "Letting go off the destructive button pressed it, and the dialog closed.");
    }

    /// <summary>
    /// A style that paints every button's presenter, fill and label, in <paramref name="pseudoClass"/>, as
    /// Fluent's button does.
    /// </summary>
    private static Style PaintedLikeFluent(string pseudoClass)
    {
        Style style = new(selector => selector
            .OfType<Button>()
            .Class(pseudoClass)
            .Template()
            .OfType<ContentPresenter>()
            .Name("PART_ContentPresenter"));
        style.Setters.Add(new Setter(ContentPresenter.BackgroundProperty, new ImmutableSolidColorBrush(Color.Parse("#E0E0E0"))));
        style.Setters.Add(new Setter(ContentPresenter.ForegroundProperty, new ImmutableSolidColorBrush(Color.Parse("#1A1A1A"))));
        return style;
    }

    private static Painted HeaderGlyph(string glyph, string key)
    {
        return new Painted(
            $"header's {glyph}",
            dialog => dialog.GetLogicalDescendants()
                            .OfType<TextBlock>()
                            .Where(block => block.GetType() == typeof(TextBlock) && block.Text == glyph)
                            .ToArray(),
            TextBlock.ForegroundProperty,
            key,
            1);
    }

    private static Painted[] DestructiveButton(string id)
    {
        return
        [
            new Painted(
                "destructive button's fill", dialog => ButtonsWithId(dialog, id), TemplatedControl.BackgroundProperty,
                "AppServices.DestructiveBackgroundBrush", 1),
            new Painted(
                "destructive button's label", dialog => ButtonsWithId(dialog, id), TemplatedControl.ForegroundProperty,
                "AppServices.DestructiveForegroundBrush", 1),
        ];
    }

    private static Painted[] MessageColours(bool codePane)
    {
        List<Painted> painted =
        [
            Runs("paths", "AppServices.LinkBrush", DialogWindows.FirstPath, DialogWindows.SecondPath),
            Runs("links", "AppServices.LinkBrush", DialogWindows.FirstLinkText, DialogWindows.SecondLinkText),
            Glyphs("📋 buttons", "DialogCopyPath", "AppServices.SubtleBrush"),
            Glyphs("↗ buttons", "DialogOpenLink", "AppServices.LinkBrush"),
        ];
        if (codePane)
        {
            painted.Add(CodePane("code pane's fill", Border.BackgroundProperty, "AppServices.CodeBlockBackgroundBrush"));
            painted.Add(CodePane("code pane's border", Border.BorderBrushProperty, "AppServices.CodeBlockBorderBrush"));
        }

        return [.. painted];
    }

    private static Painted Runs(string what, string key, params string[] texts)
    {
        return new Painted(
            what,
            dialog => dialog.GetLogicalDescendants().OfType<Run>().Where(run => texts.Contains(run.Text)).ToArray(),
            TextElement.ForegroundProperty,
            key,
            texts.Length);
    }

    private static Painted Glyphs(string what, string idPrefix, string key)
    {
        return new Painted(
            what,
            dialog => dialog.GetLogicalDescendants()
                            .OfType<Button>()
                            .Where(button => AutomationProperties.GetAutomationId(button) is { } id
                                             && id.StartsWith(idPrefix, StringComparison.Ordinal))
                            .ToArray(),
            TemplatedControl.ForegroundProperty,
            key,
            2);
    }

    private static Painted CodePane(string what, AvaloniaProperty property, string key)
    {
        return new Painted(
            what,
            dialog => dialog.GetLogicalDescendants()
                            .OfType<Border>()
                            .Where(border => border.Child is { } child
                                             && AutomationProperties.GetAutomationId(child) == "DialogCode")
                            .ToArray(),
            property,
            key,
            1);
    }

    /// <summary>Where a window shows one colour.</summary>
    /// <param name="What">What shows it, as a failure names it.</param>
    /// <param name="Find">Finds every element in the window that shows it.</param>
    /// <param name="Property">The property each element takes it in.</param>
    /// <param name="Key">The key it comes from.</param>
    /// <param name="Count">How many elements show it.</param>
    private sealed record Painted(
        string What, Func<Window, IReadOnlyList<AvaloniaObject>> Find, AvaloniaProperty Property, string Key, int Count);
}
