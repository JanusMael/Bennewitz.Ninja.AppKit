using System.Text.RegularExpressions;
using Bennewitz.Ninja.ScopedEditors.AvaloniaUI.Tests;

namespace ScopedEditors.Tests.Architecture;

/// <summary>
/// Every severity glyph in this package's markup takes its size from the severity, not from a
/// literal. Carried across from ClaudeForge's <c>SeverityGlyphFontSizeMarkupTests</c>.
/// </summary>
/// <remarks>
/// <para>
/// ⛔ <b>The size was hardcoded at nine sites in seven files across two apps</b>, which is why
/// Critical and Caution were drawn at the same point size and the louder tier came out smaller.
/// Deleting the literals once fixes nothing on its own: the next surface to render a severity
/// reaches for <c>FontSize="11"</c> because that is what the surrounding markup does. This test is
/// what makes the new site do otherwise.
/// </para>
/// <para>
/// ⚠ <b>A malformed <c>ConverterParameter</c> is invisible at runtime by design.</b>
/// <c>AppSeverityToFontSizeConverter</c> falls back rather than throwing, because a converter that
/// throws inside a template takes the whole page down. The cost is that a typo renders a plausible
/// wrong size in silence, so it has to be caught HERE or not at all, which is why the parameter is
/// checked for being a number and not merely for being present.
/// </para>
/// <para>
/// ⚠ Only the package's two sites came across, the row dot and the banner in
/// <c>PropertyEditorWrapper.axaml</c>. The other six were ClaudeForge's, and stay guarded there.
/// </para>
/// </remarks>
public sealed class SeverityGlyphFontSizeMarkupTests
{
    /// <summary>
    /// How many glyph-rendering elements must be found: the row dot and the <c>IsDangerNow</c> banner
    /// in <c>PropertyEditorWrapper.axaml</c>. Exact rather than a floor, so a discovery pattern that
    /// stops matching fails, and adding or removing a surface is a deliberate update.
    /// </summary>
    private const int ExpectedGlyphSites = 2;

    /// <summary>
    /// A self-closing <c>TextBlock</c> that names the severity glyph converter, whether in its
    /// <c>Text</c> binding or through <c>AppSeverityToGlyphConverter.GlyphFontFamily</c>. Attributes
    /// never contain a bare <c>&gt;</c> (markup extensions and bindings do not produce one), so
    /// stopping at the first is safe, and <c>[^&gt;]</c> already spans the newlines these multi-line
    /// elements are written across.
    /// </summary>
    /// <remarks>
    /// ⚠ <b>Looser than "whose Text comes from the converter", and blind to a closing tag.</b> Either
    /// mention is enough to match, so a glyph whose Text binding is broken is still counted. A glyph
    /// written as <c>&lt;TextBlock …&gt;&lt;/TextBlock&gt;</c> is not matched at all. That second gap
    /// is why <see cref="ExpectedGlyphSites"/> is exact: rewriting the row dot that way drops the count
    /// to 1, and the test fails.
    /// </remarks>
    private static readonly Regex GlyphElement = new(
        @"<TextBlock\b[^>]*?SeverityToGlyph[^>]*?/>",
        RegexOptions.Compiled);

    private static readonly Regex SizeBinding = new(
        @"Converter=\{StaticResource\s+SeverityToFontSize\}\s*,\s*ConverterParameter\s*=\s*""?(?<base>[0-9]+(?:\.[0-9]+)?)""?",
        RegexOptions.Compiled);

    private static readonly Regex HardcodedSize = new(
        @"FontSize\s*=\s*""[0-9]", RegexOptions.Compiled);

    /// <summary>
    /// ⚠ The xmlns PREFIX differs from file to file, so a literal prefix would match none of them and
    /// the check would pass over every file.
    /// </summary>
    private static readonly Regex Declaration = new(
        @"<\w+:AppSeverityToFontSizeConverter\s+x:Key\s*=\s*""SeverityToFontSize""",
        RegexOptions.Compiled);

    [Fact]
    public void Every_severity_glyph_sizes_itself_from_the_severity()
    {
        List<string> problems = [];
        int sites = 0;

        foreach (PackageSources.SourceText file in PackageSources.MarkupText())
        {
            foreach (Match element in GlyphElement.Matches(file.Text))
            {
                sites++;

                if (HardcodedSize.IsMatch(element.Value))
                {
                    problems.Add(
                        $"{file.Path} renders a severity glyph at a literal FontSize. Critical and "
                        + "Caution then draw at the same point size, and ⊗ is 20% shorter than ⚠ at "
                        + "equal points, so the loudest tier comes out the smallest.");
                    continue;
                }

                if (!SizeBinding.IsMatch(element.Value))
                {
                    problems.Add(
                        $"{file.Path} renders a severity glyph without binding SeverityToFontSize with a "
                        + "numeric ConverterParameter. The converter falls back silently on a bad "
                        + "parameter, so nothing else will tell you.");
                }
            }
        }

        MessageAssert.Equal(
            ExpectedGlyphSites,
            sites,
            $"found {sites} severity-glyph element(s) under {PackageSources.Project}, expected "
            + $"{ExpectedGlyphSites}. Either the discovery pattern has stopped matching, in which case this "
            + "test is no longer checking anything, or a surface was added or removed and the count needs "
            + "a deliberate update.");

        Assert.True(
            problems.Count == 0,
            $"{problems.Count} severity glyph(s) do not size themselves:\n  " + string.Join("\n  ", problems));
    }

    /// <summary>
    /// A binding to a resource key the file never declares resolves to nothing, leaving the glyph at
    /// its inherited size: the defect back, with the markup looking fixed.
    /// </summary>
    [Fact]
    public void Every_file_using_the_size_converter_also_declares_it()
    {
        List<string> missing = [];
        int users = 0;

        foreach (PackageSources.SourceText file in PackageSources.MarkupText())
        {
            if (!file.Text.Contains("StaticResource SeverityToFontSize", StringComparison.Ordinal))
            {
                continue;
            }

            users++;
            if (!Declaration.IsMatch(file.Text))
            {
                missing.Add(file.Path);
            }
        }

        Assert.True(
            users >= 1,
            $"no file under {PackageSources.Project} uses SeverityToFontSize. The property wrapper renders "
            + "a severity glyph, so the scan has lost its subject and would pass without checking anything.");

        Assert.True(
            missing.Count == 0,
            "these files bind SeverityToFontSize without declaring it, so the binding resolves to nothing "
            + $"and the glyph keeps its inherited size:\n  {string.Join("\n  ", missing)}");
    }
}
