using System.Text.RegularExpressions;
using Bennewitz.Ninja.ScopedEditors.AvaloniaUI.Tests;

namespace ScopedEditors.Tests.Architecture;

/// <summary>
/// Every surface in this package that renders a danger-annotated view-model renders its severity.
/// Carried across from ClaudeForge's <c>DangerSurfaceMarkupTests</c>.
/// </summary>
/// <remarks>
/// <para>
/// ⛔⛔ <b>The view-model half and the markup half never reference each other.</b> The classifier can
/// be perfectly wired, and its tests green, while a template simply does not bind it. A Critical
/// setting then renders as an ordinary row, and nothing goes red. So the markup is read.
/// </para>
/// <para>
/// ⚠ <b>Only the package's surface came across.</b> The original discovered four kinds of surface.
/// Three of them (a search hit, the effective-value rows and the save dialog) are ClaudeForge's, and
/// so is ClaudeForge's own copy of the property wrapper. A fix here does not reach that copy, and the
/// guard over it stays in that repository.
/// </para>
/// <para>
/// Reads source TEXT with its XML comments removed (see <see cref="PackageSources"/>), rather than
/// loading the markup: the question is what the source binds, and a word in a comment binds nothing.
/// </para>
/// </remarks>
public sealed class DangerSurfaceMarkupTests
{
    /// <summary>
    /// A kind of danger surface: how to recognise its templates, and how many must exist.
    /// </summary>
    /// <param name="DataType">
    /// The <c>x:DataType</c> suffix every such template declares. This is the discovery key because
    /// compiled bindings require it: a file binding these members without declaring the type would not
    /// compile.
    /// </param>
    /// <param name="Minimum">
    /// How many files must be found. A discovery scan that matches nothing passes vacuously, which is
    /// worse than no test: the zero gets quoted as evidence the surfaces are fine.
    /// </param>
    /// <param name="Description">What the surface is, for the failure message.</param>
    /// <param name="DelegatesTo">
    /// A control that already renders this surface's severity. A template whose job is to HOST that
    /// control satisfies the requirement by composition rather than by binding the members itself.
    /// <para>
    /// ⚠ <b>An escape hatch, sound only because the control it defers to is itself under guard
    /// here</b>, by the minimum count and by
    /// <see cref="The_property_wrapper_renders_the_IsDangerNow_banner"/>. A hatch needs the thing it
    /// defers to to be checked, not assumed.
    /// </para>
    /// </param>
    private sealed record Surface(string DataType, int Minimum, string Description, string? DelegatesTo = null);

    private static readonly Surface[] Surfaces =
    [
        new("PropertyEditorViewModel", 1, "the settings row", DelegatesTo: "PropertyEditorWrapper"),
    ];

    /// <summary>
    /// What a template must bind. Both, not either: the glyph without the accessible text is a
    /// sighted-only signal, and the accessible text without the glyph is invisible.
    /// </summary>
    private static readonly string[] RequiredBindings = ["HasDangerSeverity", "DangerAccessibleText"];

    [Fact]
    public void Every_danger_surface_renders_its_severity()
    {
        IReadOnlyList<PackageSources.SourceText> markup = PackageSources.MarkupText();
        List<string> problems = [];

        foreach (Surface surface in Surfaces)
        {
            List<string> found = [];

            foreach (PackageSources.SourceText file in markup)
            {
                if (!Regex.IsMatch(file.Text, $@"x:DataType\s*=\s*""[^""]*:{Regex.Escape(surface.DataType)}"""))
                {
                    continue;
                }

                found.Add(file.Path);

                // Hosting the control that draws the severity is a valid way to render it. The
                // element carries an xmlns PREFIX that differs from file to file, so a plain "<Name"
                // test would match none of them and the hatch would never open.
                //
                // ⛔⛔ A FILE CANNOT DELEGATE TO ITSELF, and this exclusion is load-bearing. The wrapper
                // renders nested object children by RECURSING into <ctrl:PropertyEditorWrapper />, so
                // without it the hatch opens for the very file it exists to check: a guard that vouches
                // for its own subject. The original caught this with a canary that removed the dot from
                // a wrapper, and the guard stayed green.
                if (surface.DelegatesTo is { } control
                    && !Regex.IsMatch(file.Text, $@"x:Class\s*=\s*""[^""]*\.{Regex.Escape(control)}""")
                    && Regex.IsMatch(file.Text, $@"<\w+:{Regex.Escape(control)}\b"))
                {
                    continue;
                }

                string[] missing = [.. RequiredBindings.Where(b => !file.Text.Contains(b, StringComparison.Ordinal))];

                if (missing.Length > 0)
                {
                    problems.Add(
                        $"{file.Path} renders {surface.Description} without its severity: missing "
                        + string.Join(", ", missing));
                }
            }

            Assert.True(
                found.Count >= surface.Minimum,
                $"expected at least {surface.Minimum} template(s) with x:DataType=\"…:{surface.DataType}\" "
                + $"under {PackageSources.Project}, found {found.Count} ({string.Join(", ", found)}). The "
                + "discovery pattern has stopped matching, and this test is no longer checking anything.");
        }

        Assert.True(
            problems.Count == 0,
            $"{problems.Count} danger surface(s) render without severity:\n  "
            + string.Join("\n  ", problems)
            + "\n\nA row with no dot tells the user the setting is unremarkable. Bind "
            + "HasDangerSeverity (visibility) and DangerAccessibleText (HelpText and tooltip).");
    }

    /// <summary>
    /// ⛔ <c>AutomationProperties.Name</c> is IGNORED on a <c>TextBlock</c>: the <c>Text</c> always
    /// wins, so a severity glyph annotated that way announces the glyph character and nothing else.
    /// Measured through UIA; see <c>docs/avalonia-gotchas.md</c> in Bennewitz.Ninja.XamlQuality.
    /// </summary>
    [Fact]
    public void The_severity_glyph_is_annotated_with_HelpText_not_Name()
    {
        List<string> offenders = [];
        List<string> checkedFiles = [];

        foreach (PackageSources.SourceText file in PackageSources.MarkupText())
        {
            if (!file.Text.Contains("DangerAccessibleText", StringComparison.Ordinal))
            {
                continue;
            }

            checkedFiles.Add(file.Path);
            if (Regex.IsMatch(file.Text, @"AutomationProperties\.Name\s*=\s*""\{Binding DangerAccessibleText\}"""))
            {
                offenders.Add(file.Path);
            }
        }

        Assert.True(
            checkedFiles.Count >= 1,
            $"no file under {PackageSources.Project} binds DangerAccessibleText. The property wrapper "
            + "carries it, so the scan has lost its subject and would pass without checking anything.");

        Assert.True(
            offenders.Count == 0,
            "AutomationProperties.Name is ignored on a TextBlock (its Text wins), so these annotations "
            + $"announce nothing:\n  {string.Join("\n  ", offenders)}\n\n"
            + "Use AutomationProperties.HelpText. Do not wrap the glyph to work around it: a Border and a "
            + "ContentControl both get no automation peer at all.");
    }

    /// <summary>
    /// The wrapper renders the banner, not just the dot.
    /// </summary>
    /// <remarks>
    /// ⚠ The dot and the banner answer different questions: the tier, versus "the value held right now
    /// is the unsafe one". A wrapper carrying only the dot silently drops the warning that matters
    /// most, on the rows where something is actually wrong.
    /// </remarks>
    [Fact]
    public void The_property_wrapper_renders_the_IsDangerNow_banner()
    {
        PackageSources.SourceText[] wrappers =
        [
            .. PackageSources.MarkupText()
                .Where(f => f.Path.EndsWith("/PropertyEditorWrapper.axaml", StringComparison.Ordinal)),
        ];

        MessageAssert.Equal(
            1,
            wrappers.Length,
            $"expected exactly 1 PropertyEditorWrapper.axaml under {PackageSources.Project}, found "
            + $"{wrappers.Length}: {string.Join(", ", wrappers.Select(w => w.Path))}");

        Assert.True(
            wrappers[0].Text.Contains("IsDangerNow", StringComparison.Ordinal),
            $"{wrappers[0].Path} has no IsDangerNow banner. The dot alone says the setting matters; only "
            + "the banner says the current value is wrong.");
    }
}
