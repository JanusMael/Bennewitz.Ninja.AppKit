using System.Globalization;
using Bennewitz.Ninja.ScopedEditors.AvaloniaUI.Converters;

namespace Bennewitz.Ninja.ScopedEditors.AvaloniaUI.Tests.Converters;

/// <summary>
/// The glyph half of the dual-coded severity indicator.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>The distinctness test is the point of this file.</b> Colour and shape are meant to carry
/// the same information independently; if two severities shared a glyph, the indicator would
/// silently collapse back to colour-only for exactly the users the dual-coding exists for —
/// Critical and Caution sit on the red-green axis.
/// </para>
/// </remarks>
public sealed class AppSeverityToGlyphConverterTests
{
    private static object Convert(object? value) =>
        AppSeverityToGlyphConverter.Instance.Convert(
            value, typeof(string), null, CultureInfo.InvariantCulture);

    [Fact]
    public void EverySeverityGetsADistinctGlyph()
    {
        AppSeverity[] all = Enum.GetValues<AppSeverity>();
        string[] glyphs = [.. all.Select(AppSeverityToGlyphConverter.GlyphFor)];

        MessageAssert.Equal(all.Length, glyphs.Distinct(StringComparer.Ordinal).Count(), "two severities share a glyph, so shape no longer distinguishes them and the "
            + $"indicator is colour-only: {string.Join(" ", all.Zip(glyphs, (s, g) => $"{s}={g}"))}");
    }

    [Fact]
    public void NoGlyphIsEmptyOrWhitespace()
    {
        foreach (AppSeverity s in Enum.GetValues<AppSeverity>())
        {
            Assert.False(string.IsNullOrWhiteSpace(AppSeverityToGlyphConverter.GlyphFor(s)), $"{s} has no glyph, so its dot renders as blank space");
        }
    }

    /// <remarks>
    /// ⚠ Emoji need a system emoji font and render as tofu without one — a documented Linux
    /// problem in Bennewitz.Ninja.XamlQuality's <c>docs/avalonia-gotchas.md</c>. Every glyph must
    /// stay in the Basic Multilingual Plane, which is what rules emoji out: they live above U+FFFF
    /// and arrive as surrogate pairs.
    /// </remarks>
    [Fact]
    public void EveryGlyphIsASingleBmpCharacterNotAnEmoji()
    {
        foreach (AppSeverity s in Enum.GetValues<AppSeverity>())
        {
            string g = AppSeverityToGlyphConverter.GlyphFor(s);

            MessageAssert.Equal(1, g.Length, $"{s}'s glyph '{g}' is {g.Length} UTF-16 units. A surrogate pair means an "
                + "astral-plane character — almost certainly an emoji, which needs a font this "
                + "app cannot assume.");
            Assert.False(char.IsSurrogate(g[0]), $"{s}'s glyph is a surrogate");
        }
    }

    [Fact]
    public void TheConverterAgreesWithGlyphFor()
    {
        foreach (AppSeverity s in Enum.GetValues<AppSeverity>())
        {
            MessageAssert.Equal(AppSeverityToGlyphConverter.GlyphFor(s), Convert(s), "the static helper is what tests assert against; it must be the same mapping the "
                + "converter uses, or the tests stop describing the UI");
        }
    }

    [Fact]
    public void ANonSeverityValueFallsBackToNeutralRatherThanThrowing()
    {
        // A binding can legitimately deliver null before the view-model settles; a throwing
        // converter would surface as a broken row rather than a missing dot.
        Assert.Equal(AppSeverityToGlyphConverter.GlyphFor(AppSeverity.Neutral), Convert(null));
        Assert.Equal(AppSeverityToGlyphConverter.GlyphFor(AppSeverity.Neutral), Convert("nonsense"));
    }

    [Fact]
    public void ConvertBackIsNotSupported()
    {
        Assert.Throws<NotSupportedException>(() =>
            AppSeverityToGlyphConverter.Instance.ConvertBack(
                "▲", typeof(AppSeverity), null, CultureInfo.InvariantCulture));
    }
}
