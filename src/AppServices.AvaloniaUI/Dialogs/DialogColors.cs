using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Styling;

namespace Bennewitz.Ninja.AppServices.AvaloniaUI.Dialogs;

/// <summary>
/// The colours of the windows the package builds: ten keys under the <c>AppServices.</c> prefix, the
/// dictionary that declares each for Light, Dark and Default, and the one helper a window reads a colour
/// through (<c>plans/00007</c>, decisions 6 and 7).
/// </summary>
/// <remarks>
/// <para>
/// An instance is a resource dictionary whose Light, Dark and Default theme dictionaries each declare every
/// key, the Default holding the Light values, so a theme variant that inherits neither still finds each.
/// Light keeps the colours the dialogs had before; Dark is chosen for a dark window.
/// </para>
/// <para>
/// <see cref="Brush"/> is the one way a window reads a colour: a dynamic resource, so an open window follows
/// a change of theme and of the application's resources. Its first call in an application merges a new
/// instance into the application's resources, first among its merged dictionaries, unless one is there
/// already. Avalonia reads merged dictionaries from the last to the first, so a key a host declares among its
/// application's own resources, or in a dictionary it merges before or after, is found first. An instance is
/// never shared between applications, since a dictionary takes one owner and throws in a second, and whether
/// one is there is read from the merged dictionaries, never from a flag.
/// </para>
/// <para>
/// ⚠ A window built to show before the application's resources have loaded, as
/// <see cref="FatalErrorDialog"/> is, never calls <see cref="Brush"/>.
/// </para>
/// </remarks>
internal sealed class DialogColors : ResourceDictionary
{
    /// <summary>The glyph before an error's title.</summary>
    internal const string ErrorBrush = "AppServices.ErrorBrush";

    /// <summary>The glyph before a destructive action's title.</summary>
    internal const string WarningBrush = "AppServices.WarningBrush";

    /// <summary>A path, a link, and the ↗ after a link.</summary>
    internal const string LinkBrush = "AppServices.LinkBrush";

    /// <summary>The 📋 after a path.</summary>
    internal const string SubtleBrush = "AppServices.SubtleBrush";

    /// <summary>A destructive button's fill at rest.</summary>
    internal const string DestructiveBackgroundBrush = "AppServices.DestructiveBackgroundBrush";

    /// <summary>A destructive button's fill while it is pointed at.</summary>
    internal const string DestructivePointerOverBackgroundBrush = "AppServices.DestructivePointerOverBackgroundBrush";

    /// <summary>A destructive button's fill while it is pressed.</summary>
    internal const string DestructivePressedBackgroundBrush = "AppServices.DestructivePressedBackgroundBrush";

    /// <summary>A destructive button's label, in every state.</summary>
    internal const string DestructiveForegroundBrush = "AppServices.DestructiveForegroundBrush";

    /// <summary>A code pane's fill.</summary>
    internal const string CodeBlockBackgroundBrush = "AppServices.CodeBlockBackgroundBrush";

    /// <summary>A code pane's border.</summary>
    internal const string CodeBlockBorderBrush = "AppServices.CodeBlockBorderBrush";

    /// <summary>Each key, with its colour in Light and in Dark.</summary>
    private static readonly (string Key, string Light, string Dark)[] Declared =
    [
        (ErrorBrush, "#C62828", "#F85149"),
        (WarningBrush, "#C77700", "#D29922"),
        (LinkBrush, "#1F6FEB", "#58A6FF"),
        (SubtleBrush, "#888888", "#8B949E"),
        (DestructiveBackgroundBrush, "#C62828", "#C62828"),
        (DestructivePointerOverBackgroundBrush, "#B71C1C", "#B71C1C"),
        (DestructivePressedBackgroundBrush, "#8E1B1B", "#8E1B1B"),
        (DestructiveForegroundBrush, "#FFFFFF", "#FFFFFF"),
        (CodeBlockBackgroundBrush, "#F4F4F4", "#1F1F1F"),
        (CodeBlockBorderBrush, "#D0D0D0", "#3A3A3A"),
    ];

    /// <summary>Declares every key in a Light, a Dark and a Default theme dictionary.</summary>
    internal DialogColors()
    {
        ThemeDictionaries[ThemeVariant.Light] = Theme(declared => declared.Light);
        ThemeDictionaries[ThemeVariant.Dark] = Theme(declared => declared.Dark);
        ThemeDictionaries[ThemeVariant.Default] = Theme(declared => declared.Light);
    }

    /// <summary>
    /// The fill of a control that paints nothing and still takes the pointer: transparent, since a null fill
    /// takes no pointer.
    /// </summary>
    internal static IBrush NoFill => Brushes.Transparent;

    /// <summary>
    /// The colour <paramref name="key"/> names, as a binding that follows a change of theme and of the
    /// application's resources; the first call in an application merges a new instance of this dictionary
    /// into its resources.
    /// </summary>
    /// <param name="key">One of this class's keys.</param>
    /// <returns>A dynamic resource of <paramref name="key"/>, for a property or a style's setter.</returns>
    internal static BindingBase Brush(string key)
    {
        MergeInto(Application.Current);
        return new DynamicResourceExtension(key);
    }

    /// <summary>
    /// Puts a new instance first among <paramref name="application"/>'s merged dictionaries, unless one is
    /// there already.
    /// </summary>
    private static void MergeInto(Application? application)
    {
        if (application is null)
        {
            return;
        }

        IList<IResourceProvider> merged = application.Resources.MergedDictionaries;
        if (merged.OfType<DialogColors>().Any())
        {
            return;
        }

        merged.Insert(0, new DialogColors());
    }

    /// <summary>A theme dictionary declaring every key, each with the colour <paramref name="pick"/> chooses.</summary>
    private static ResourceDictionary Theme(Func<(string Key, string Light, string Dark), string> pick)
    {
        ResourceDictionary theme = new();
        foreach ((string Key, string Light, string Dark) declared in Declared)
        {
            theme.Add(declared.Key, new ImmutableSolidColorBrush(Color.Parse(pick(declared))));
        }

        return theme;
    }
}
