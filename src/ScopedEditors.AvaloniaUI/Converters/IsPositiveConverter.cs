using Avalonia.Data.Converters;

namespace Bennewitz.Ninja.ScopedEditors.AvaloniaUI.Converters;

/// <summary>
/// Returns <c>true</c> when the input integer is greater than zero.
/// Used to show/hide count badges in the Hooks editor and other list views.
/// </summary>
public static class IsPositiveConverter
{
    /// <summary>The shared instance. This converter is stateless, so one serves
    /// every binding and markup can reference it with <c>x:Static</c>.</summary>
    public static readonly IValueConverter Instance =
        new FuncValueConverter<int?, bool>(n => n is > 0);
}