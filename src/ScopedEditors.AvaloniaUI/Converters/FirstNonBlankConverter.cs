using System.Globalization;
using Avalonia.Data.Converters;

namespace Bennewitz.Ninja.ScopedEditors.AvaloniaUI.Converters;

/// <summary>
/// The first of a <c>MultiBinding</c>'s values that is a string with something in it other than
/// white space, or <see langword="null"/> when none is. The bindings are listed in the order they are
/// preferred.
/// </summary>
/// <remarks>
/// <para>
/// <c>PropertyEditorWrapper.axaml</c> gives each value input a tooltip of the property's
/// <c>Description</c>, or its <c>DisplayName</c> when it has none, by binding the two in that order.
/// The choice is made in the markup, so the view-models stay free of anything a tooltip needs.
/// </para>
/// <para>
/// ⚠ <b>A blank string counts as none.</b> A description that is empty or white space would put an
/// empty box under the pointer, where the name at least says what the input sets.
/// </para>
/// </remarks>
public sealed class FirstNonBlankConverter : IMultiValueConverter
{
    /// <inheritdoc/>
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        foreach (object? value in values)
        {
            if (value is string text && !string.IsNullOrWhiteSpace(text))
            {
                return text;
            }
        }

        return null;
    }
}
