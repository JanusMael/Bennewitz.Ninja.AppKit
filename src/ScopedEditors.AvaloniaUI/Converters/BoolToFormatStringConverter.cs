using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;

namespace Bennewitz.Ninja.ScopedEditors.AvaloniaUI.Converters;

/// <summary>
/// Converts a bool <c>IsInteger</c> value to a <see cref="NumericUpDown"/>
/// format string: <c>true</c> → <c>"0"</c> (integer), <c>false</c> → <c>"0.##"</c> (decimal).
/// </summary>
public sealed class BoolToFormatStringConverter : IValueConverter
{
    /// <summary>The shared instance. This converter is stateless, so one serves
    /// every binding and markup can reference it with <c>x:Static</c>.</summary>
    public static readonly BoolToFormatStringConverter Instance = new();

    /// <inheritdoc/>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? "0" : "0.##";
    }

    /// <inheritdoc/>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}