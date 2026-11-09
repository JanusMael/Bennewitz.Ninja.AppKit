using Bennewitz.Ninja.ScopedEditors.AvaloniaUI.Controls;
using Bennewitz.Ninja.ScopedEditors.AvaloniaUI.Tests;
using Bennewitz.Ninja.XamlQuality;
using Bennewitz.Ninja.XamlQuality.Rules;

namespace ScopedEditors.Tests.Accessibility;

/// <summary>
/// The rows a list generates from <c>ItemsSource</c> are named by what they show (<c>BNXQ1009</c>) and
/// draw a background of their own (<c>BNXQ1010</c>). Adopted 2026-11-09, with XamlQuality
/// <c>2026.4.1109</c>.
/// </summary>
/// <remarks>
/// <para>
/// The one such list today is the strict enum's <c>ComboBox</c> in
/// <c>Controls/PropertyEditorWrapper.axaml</c>, whose rows show <c>EnumOption</c>s. Both rules
/// passed over it on adoption. XamlQuality's other rules this package does not run, <c>BNXQ1003</c> to
/// <c>BNXQ1006</c> and <c>BNXQ1008</c>, inspected nothing in this markup that day: it has no control
/// themes or templates, no key bindings, and no fixed or zero-size grid slots.
/// </para>
/// <para>
/// ⚠ Both rules read which container a control generates, and which peer names it, from compiled
/// types, so the scan is given the package's assembly. Without it every <c>ItemsSource</c> is skipped,
/// which is why <c>Skipped</c> must be empty: a skipped list is one the rule did not decide.
/// </para>
/// </remarks>
public sealed class GeneratedRowRuleTests
{
    private static XamlScanContext Scan() =>
        PackageSources.Markup().WithAssemblies(typeof(PropertyEditorWrapper).Assembly);

    [Fact]
    public void Every_generated_row_is_named_by_what_it_shows()
    {
        XamlRuleResult result = new ItemContainerNameRule().Analyze(Scan());

        AssertDecided(result, "BNXQ1009");
        MessageAssert.Equal(
            0,
            result.Findings.Count,
            $"{result.Findings.Count} of {result.Inspected} list(s) generate rows a screen reader names by "
            + "their item's type rather than what they show. Offenders:\n  " + string.Join("\n  ", result.Findings));
    }

    [Fact]
    public void Every_generated_row_draws_a_background_of_its_own()
    {
        XamlRuleResult result = new ItemContainerBackgroundRule().Analyze(Scan());

        AssertDecided(result, "BNXQ1010");
        MessageAssert.Equal(
            0,
            result.Findings.Count,
            $"{result.Findings.Count} of {result.Inspected} list(s) generate rows that take a click only "
            + "where something they hold draws. Offenders:\n  " + string.Join("\n  ", result.Findings));
    }

    /// <summary>
    /// ⛔ The premise, asserted rather than assumed: a rule that inspected nothing, or skipped what it
    /// found, would report success having decided nothing.
    /// </summary>
    private static void AssertDecided(XamlRuleResult result, string id)
    {
        Assert.True(
            result.Inspected > 0,
            $"{id} inspected no list under {PackageSources.Project}. The enum ComboBox in "
            + "PropertyEditorWrapper.axaml generates rows from ItemsSource; either the markup moved or "
            + "the scan is looking in the wrong place.");
        Assert.True(
            result.Skipped.Count == 0,
            $"{id} could not decide {result.Skipped.Count} list(s):\n  " + string.Join("\n  ", result.Skipped));
    }
}
