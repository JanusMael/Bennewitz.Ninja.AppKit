using System.Globalization;
using Avalonia.Data;
using Bennewitz.Ninja.ScopedEditors.AvaloniaUI.Converters;

namespace Bennewitz.Ninja.ScopedEditors.AvaloniaUI.Tests.Converters;

/// <summary>
/// Pins the contract of <see cref="SuppressNullWriteConverter"/>: identity in
/// the Convert direction, and <see cref="BindingOperations.DoNothing"/> for
/// null inputs in the ConvertBack direction. This is what stops the ComboBox
/// cross-product-scope-bleed InvalidCastException in
/// <c>SettingsGroupEditorView</c>.
/// </summary>
public sealed class SuppressNullWriteConverterTests
{
    private static object? Convert(object? value)
    {
        return SuppressNullWriteConverter.Instance.Convert(
            value, typeof(object), parameter: null, CultureInfo.InvariantCulture);
    }

    private static object? ConvertBack(object? value)
    {
        return SuppressNullWriteConverter.Instance.ConvertBack(
            value, typeof(object), parameter: null, CultureInfo.InvariantCulture);
    }

    // -------------------------------------------------------------------
    // Convert — pure identity in every case.
    // -------------------------------------------------------------------

    [Fact]
    public void Convert_PassesNullThrough()
    {
        Assert.Null(Convert(null));
    }

    [Fact]
    public void Convert_PassesEnumValueThrough()
    {
        // StringComparison is a convenient system enum that needs no extra reference.
        Assert.Equal(StringComparison.Ordinal, Convert(StringComparison.Ordinal));
    }

    [Fact]
    public void Convert_PassesStringThrough()
    {
        Assert.Equal("hello", Convert("hello"));
    }

    // -------------------------------------------------------------------
    // ConvertBack — null ⇒ BindingOperations.DoNothing, else identity.
    // -------------------------------------------------------------------

    [Fact]
    public void ConvertBack_NullTargetYieldsDoNothingSentinel()
    {
        Assert.Same(BindingOperations.DoNothing, ConvertBack(null));
    }

    [Fact]
    public void ConvertBack_NonNullTargetReturnsSameInstance()
    {
        StringComparison value = StringComparison.CurrentCulture;
        Assert.Equal(value, ConvertBack(value));
    }

    [Fact]
    public void ConvertBack_ReferenceTypeReturnedUnchanged()
    {
        object value = new();
        Assert.Same(value, ConvertBack(value));
    }

    [Fact]
    public void Instance_IsSingleton()
    {
        // MSTEST0032: flagged as always-true because both sides are syntactically
        // identical. That is the point — this pins Instance as a true singleton.
        // If it ever became `=> new SuppressNullWriteConverter()`, this would fail.
#pragma warning disable MSTEST0032
        Assert.Same(SuppressNullWriteConverter.Instance, SuppressNullWriteConverter.Instance);
#pragma warning restore MSTEST0032
    }
}