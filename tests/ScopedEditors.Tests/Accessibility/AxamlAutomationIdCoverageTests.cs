using Bennewitz.Ninja.ScopedEditors.AvaloniaUI.Tests;
using Bennewitz.Ninja.XamlQuality;
using Bennewitz.Ninja.XamlQuality.Rules;

namespace ScopedEditors.Tests.Accessibility;

/// <summary>
/// Every interactive control in this package's markup declares an explicit
/// <c>AutomationProperties.AutomationId</c>: the key a test or an agent searches by, where the name
/// <see cref="AxamlAccessibilityCoverageTests"/> requires is what a person hears. XamlQuality's
/// <c>BNXQ1007</c>, adopted 2026-09-28.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>The ids are bound to the property's schema path.</b> <c>PropertyEditorWrapper</c> renders once
/// per property, so a fixed id would repeat on every row of a settings page. An editor's value input
/// takes the path itself, and every other control on the row takes the path, a <c>#</c>, and what
/// the control does, so a test finds the input for <c>permissions.defaultMode</c> by that string and
/// its reset button by <c>permissions.defaultMode#reset</c>.
/// </para>
/// <para>
/// ⚠ <b>Declared, not unique.</b> The rule can show that each control declares an id, bound or not.
/// It cannot show that the ids differ from row to row; the path binding is what makes them differ.
/// </para>
/// <para>
/// ⛔ <b><c>x:Name</c> is not an id.</b> Avalonia derives one from it, so a control with only
/// <c>x:Name</c> reports that name, and renaming the field would rename what every test searches
/// for. The rule does not count it.
/// </para>
/// </remarks>
public sealed class AxamlAutomationIdCoverageTests
{
    /// <summary>
    /// The premise: at least this many controls are examined. <c>PropertyEditorWrapper.axaml</c> holds
    /// thirteen that <c>BNXQ1007</c> covers, so ordinary churn does not trip it and a scan gone blind
    /// does.
    /// </summary>
    private const int MinimumInteractiveControls = 10;

    [Fact]
    public void Every_interactive_control_in_the_markup_declares_an_automation_id()
    {
        XamlRuleResult result =
            new InteractiveAutomationIdRule(AxamlAccessibilityCoverageTests.BeyondTheFrameworkList)
                .Analyze(PackageSources.Markup());

        Assert.True(
            result.Inspected >= MinimumInteractiveControls,
            $"BNXQ1007 examined {result.Inspected} interactive control(s) under {PackageSources.Project}, "
            + $"expected at least {MinimumInteractiveControls}. The scan has stopped seeing the controls, "
            + "so a clean result would mean nothing.");

        Assert.True(
            result.Findings.Count == 0,
            $"{result.Findings.Count} of {result.Inspected} interactive control(s) under "
            + $"{PackageSources.Project} declare no automation id:\n  "
            + string.Join("\n  ", result.Findings)
            + "\n\nGive each one AutomationProperties.AutomationId. An editor's value input binds the "
            + "property's Path; every other control on the row binds Path with a '#' and what it does, "
            + "as in {Binding Path, StringFormat='{}{0}#reset'}.");
    }
}
