using Bennewitz.Ninja.ScopedEditors.AvaloniaUI.Tests;
using Bennewitz.Ninja.XamlQuality;
using Bennewitz.Ninja.XamlQuality.Rules;

namespace ScopedEditors.Tests.Accessibility;

/// <summary>
/// Every <c>&lt;Expander&gt;</c> in this package's markup declares <c>AutomationProperties.Name</c>.
/// Carried across from ClaudeForge's test of the same name, which reached this markup through
/// OpenForge2k's <c>src/</c> and was left behind with the other markup guards.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>This is the precondition of a style, not a preference.</b>
/// <c>Themes/AccessibilityNames.axaml</c> copies an Expander's name down to its <c>ExpanderHeader</c>
/// part, because the part is what takes focus and the theme gives it a Grid as content. With no name
/// to copy, the setter applies nothing, silently, and a screen reader hears
/// <c>"Avalonia.Controls.Grid, button"</c> (finding <c>F10</c>).
/// <c>Themes/TemplatePartAutomationNameTests</c> proves the style moves a name that exists; this is
/// what makes one exist.
/// </para>
/// <para>
/// ⚠ <b>Kept apart from <see cref="AxamlAccessibilityCoverageTests"/></b>, as XamlQuality keeps
/// <c>BNXQ1001</c> apart from <c>BNXQ1002</c>: an unnamed Expander has a failure mode beyond being
/// unnamed, so it gets the exact question on its own.
/// </para>
/// </remarks>
public sealed class ExpanderAutomationNameTests
{
    [Fact]
    public void Every_expander_in_the_markup_declares_an_automation_name()
    {
        XamlRuleResult result = new ExpanderAutomationNameRule().Analyze(PackageSources.Markup());

        // ⛔ The premise, asserted rather than assumed: a scan that found no Expanders would report
        // success having measured nothing. PropertyEditorWrapper.axaml holds one, for object editors.
        Assert.True(
            result.Inspected > 0,
            $"No <Expander> anywhere under {PackageSources.Project}. Either the markup moved or the scan "
            + "is looking in the wrong place; a green result here would mean nothing.");

        MessageAssert.Equal(
            0,
            result.Findings.Count,
            $"{result.Findings.Count} of {result.Inspected} Expander(s) declare no automation name. Each "
            + "one's header part will announce 'Avalonia.Controls.Grid' to a screen reader, because the "
            + "inherited-name style in Themes/AccessibilityNames.axaml has no name to copy and applies "
            + "nothing. Offenders:\n  " + string.Join("\n  ", result.Findings));
    }
}
