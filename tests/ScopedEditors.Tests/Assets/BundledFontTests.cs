using System.Reflection;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace Bennewitz.Ninja.ScopedEditors.AvaloniaUI.Tests.Assets;

/// <summary>
/// Guards the URIs hosts use to reach the bundled fonts, such as
/// <c>avares://ScopedEditors.AvaloniaUI/Assets/Fonts#JetBrains Mono NL</c>.
/// </summary>
/// <remarks>
/// <para>
/// ⛔ <b>Nothing else in this repository resolves these URIs.</b> The fonts ship for hosts, and only
/// a host's markup names them, so a broken URI passes every other test here and fails in the host:
/// as an <c>InvalidOperationException</c> ("Could not create glyphTypeface") the first time text in
/// the family is laid out, or, behind a fallback list, as the wrong face with no signal at all. The
/// rename to <c>ScopedEditors.AvaloniaUI</c> changed every one of them.
/// </para>
/// <para>
/// ⭐ <b>Text is LAID OUT, not merely resolved.</b> An asset check proves a file is present, not
/// that the name after the <c>#</c> matches the font inside it. Laying out text is what a host
/// does, so it fails the way a host would. The headless platform's own drawing is enough: measured,
/// a stale URI throws under it exactly as under Skia.
/// </para>
/// </remarks>
public sealed class BundledFontTests
{
    /// <summary>The prefix ScopedEditors.AvaloniaUI.csproj documents beside the font files.</summary>
    private const string FontsUri = "avares://ScopedEditors.AvaloniaUI/Assets/Fonts";

    private static HeadlessUnitTestSession Session =>
        HeadlessUnitTestSession.GetOrStartForAssembly(Assembly.GetExecutingAssembly());

    /// <summary>
    /// One row per bundled file.
    /// <para>
    /// ⚠ A weight whose file is missing does not fail to resolve. The nearest face stands in and
    /// reports ITS weight: measured, 600 asked of the family with no SemiBold file came back as
    /// 700. So each row asserts the weight it got, and a font dropped from the project fails its
    /// own row rather than quietly borrowing a neighbour.
    /// </para>
    /// <para>
    /// ⓘ The SemiBold face reports its legacy family name, <c>JetBrains Mono NL SemiBold</c>,
    /// although it resolves from <c>#JetBrains Mono NL</c> like the others. Hence the name each
    /// row expects to see, rather than one derived from the URI.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("JetBrains Mono NL", 400, "JetBrains Mono NL")]
    [InlineData("JetBrains Mono NL", 600, "JetBrains Mono NL SemiBold")]
    [InlineData("JetBrains Mono NL", 700, "JetBrains Mono NL")]
    [InlineData("JetBrains Mono", 400, "JetBrains Mono")]
    [InlineData("JetBrains Mono", 700, "JetBrains Mono")]
    public Task Each_bundled_face_shapes_text_by_its_documented_uri(string family, int weight, string reportedFamily)
    {
        return Session.Dispatch(() =>
        {
            string uri = $"{FontsUri}#{family}";
            Typeface typeface = new(FontFamily.Parse(uri), FontStyle.Normal, (FontWeight)weight);

            string shapedFamily;
            FontWeight shapedWeight;
            try
            {
                TextLayout layout = new("abc", typeface, 12, Brushes.Black);
                ShapedTextRun run = layout.TextLines[0].TextRuns.OfType<ShapedTextRun>().First();
                shapedFamily = run.GlyphRun.GlyphTypeface.FamilyName;
                shapedWeight = run.GlyphRun.GlyphTypeface.Weight;
            }
            catch (InvalidOperationException ex)
            {
                Assert.Fail($"'{uri}' no longer resolves: {ex.Message}\n\nEvery host that names this family "
                    + "throws the same exception the first time it lays out text in it. If the assembly or "
                    + "the folder moved, the URI in ScopedEditors.AvaloniaUI.csproj and every host's markup "
                    + "must move with it.");
                return;
            }

            MessageAssert.Equal(reportedFamily, shapedFamily, $"'{uri}' at weight {weight} shaped text with '{shapedFamily}'.");
            MessageAssert.Equal(weight, (int)shapedWeight, $"'{uri}' at weight {weight} shaped text with a "
                + $"weight-{(int)shapedWeight} face. The file for that weight is missing from the package, and a "
                + "host asking for it silently gets another face.");
        }, CancellationToken.None);
    }
}
