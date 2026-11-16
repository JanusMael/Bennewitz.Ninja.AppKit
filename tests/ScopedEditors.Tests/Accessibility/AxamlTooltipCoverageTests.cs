using Bennewitz.Ninja.ScopedEditors.AvaloniaUI.Tests;
using Bennewitz.Ninja.XamlQuality;
using Bennewitz.Ninja.XamlQuality.Rules;

namespace ScopedEditors.Tests.Accessibility;

/// <summary>
/// Every input control in this package's markup declares a tooltip, which says what the control does
/// to the person pointing at it: the name <see cref="AxamlAccessibilityCoverageTests"/> requires
/// reaches a screen reader and the id <see cref="AxamlAutomationIdCoverageTests"/> requires reaches a
/// test, and neither is shown. XamlQuality's <c>BNXQ1011</c>, adopted 2026-11-16 with XamlQuality
/// <c>2026.4.1111</c> (<c>plans/00005</c>).
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>A value input's tooltip is the property's <c>Description</c>, or its <c>DisplayName</c> when
/// it has none.</b> The rule sees only that a tooltip is declared, and a tip bound to a missing
/// description is declared and renders nothing, so <c>Controls/PropertyEditorWrapperTooltipTests</c>
/// reads the tips off rendered rows.
/// </para>
/// <para>
/// ⚠ <b>The count is pinned, not a floor.</b> Twelve on adoption: the six value inputs, Browse, the
/// string array's ×, new-item box and Add, and the Reset and ▾ buttons. A new input control raises
/// it, and a scan that stopped seeing one lowers it.
/// </para>
/// <para>
/// ⛔ <b><c>Skipped</c> must be empty.</b> The rule skips, rather than reports, a control without a
/// tooltip of its own that a style's tooltip setter may reach, since it does not decide whether the
/// style's selector holds. A skipped control is one nobody decided.
/// </para>
/// <para>
/// The rule runs as Templates runs it, without the four element names the naming and id tests add:
/// none occurs in this markup, and a tree, a tab or a link shows its label already, as a menu item
/// does, which the rule leaves out for that reason.
/// </para>
/// </remarks>
public sealed class AxamlTooltipCoverageTests
{
    /// <summary>The input controls in <c>PropertyEditorWrapper.axaml</c>, the package's only ones.</summary>
    private const int InputControls = 12;

    [Fact]
    public void Every_input_control_in_the_markup_declares_a_tooltip()
    {
        XamlRuleResult result = new InputTooltipRule().Analyze(PackageSources.Markup());

        // Skipped before the count: a skipped control is not counted as inspected (measured: one
        // planted, the count fell to 11), so the count would report it as a control the scan stopped
        // seeing.
        Assert.True(
            result.Skipped.Count == 0,
            $"BNXQ1011 could not decide {result.Skipped.Count} input control(s), each without a tooltip of "
            + "its own and reached by a style that may give one:\n  " + string.Join("\n  ", result.Skipped)
            + "\n\nGive each one ToolTip.Tip where it is declared, so the rule decides it.");

        Assert.True(
            result.Inspected == InputControls,
            $"BNXQ1011 inspected {result.Inspected} input control(s) under {PackageSources.Project}, where "
            + $"{InputControls} are expected. More means an input control was added: give it a tooltip and "
            + "raise the count. Fewer means the scan stopped seeing one, so a clean result would cover less "
            + "than it claims.");

        Assert.True(
            result.Findings.Count == 0,
            $"{result.Findings.Count} of {result.Inspected} input control(s) under {PackageSources.Project} "
            + "declare no tooltip, so nothing tells the person pointing at one what it does:\n  "
            + string.Join("\n  ", result.Findings)
            + "\n\nGive each one ToolTip.Tip. A value input binds the property's Description and then its "
            + "DisplayName through FirstNonBlankConverter, and sets ToolTip.ShowOnDisabled; fixed chrome "
            + "takes a WrapperStrings Tip value ({x:Static loc:WrapperStrings.…}), so a host's Resolver "
            + "localises it with the rest.");
    }
}
