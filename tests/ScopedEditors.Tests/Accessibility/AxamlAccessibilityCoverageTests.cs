using Bennewitz.Ninja.ScopedEditors.AvaloniaUI.Tests;
using Bennewitz.Ninja.XamlQuality;
using Bennewitz.Ninja.XamlQuality.Rules;

namespace ScopedEditors.Tests.Accessibility;

/// <summary>
/// Every interactive control in this package's markup declares a non-blank
/// <c>AutomationProperties.Name</c>, so a screen reader announces what it is for rather than what
/// type it is. Carried across from ClaudeForge's <c>AxamlAccessibilityCoverageTests</c>, which named
/// <c>LayeredEditors.Avalonia</c> among the projects its scan had to reach.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>The check is XamlQuality's <c>XQ1002</c>, not a copy of the original scan.</b> OpenForge2k
/// had already moved its Expander guard onto that library, for the reason that applies here too: a
/// second implementation of a rule another repository maintains is the copy that learns a different
/// subset of the cases. It also accepts a name written as a property ELEMENT, which the original
/// reported as missing.
/// </para>
/// <para>
/// ⚠ <b>Coverage did not narrow in the move.</b> The original counted four elements that
/// <c>XQ1002</c>'s framework list does not, so they are passed in. The fifth, <c>Expander</c>, is
/// <c>XQ1001</c>'s, in <see cref="ExpanderAutomationNameTests"/>: XQ1002 leaves it out so that one
/// defect is not reported twice.
/// </para>
/// <para>
/// ⭐ <b>Strict zero, and no baseline.</b> The original held each file to a ratcheting baseline while
/// the backfill landed, and said what to do once the debt reached zero: delete the baseline and its
/// tracker, and let zero be the rule. This package's markup was already at zero there.
/// </para>
/// <para>
/// ⓘ Blind to control-template parts by construction, since a part has no element in any markup.
/// <c>Themes/TemplatePartAutomationNameTests</c> builds those controls and asks their peers instead.
/// </para>
/// </remarks>
public sealed class AxamlAccessibilityCoverageTests
{
    /// <summary>The elements the original counted as interactive that <c>XQ1002</c> does not.</summary>
    /// <remarks>
    /// Added to the original on 2026-08-27, after UI Automation found a navigation tree with an empty
    /// name in a file the scan had scored as fully named. None occurs in this markup today; listing
    /// them holds the next one to the same rule.
    /// </remarks>
    private static readonly string[] BeyondTheFrameworkList = ["TreeView", "TabControl", "TabItem", "HyperlinkButton"];

    /// <summary>
    /// The directories that hold this package's markup, relative to the project. A scan that stops
    /// reaching one has narrowed. Moving or renaming one means updating this list in the same commit,
    /// deliberately rather than by accident.
    /// </summary>
    private static readonly string[] DirectoriesThatMustContributeMarkup = ["Controls", "Themes"];

    /// <summary>
    /// The premise: at least this many controls are examined. <c>PropertyEditorWrapper.axaml</c> holds
    /// twelve that <c>XQ1002</c> covers, so ordinary churn does not trip it and a scan gone blind does.
    /// </summary>
    private const int MinimumInteractiveControls = 10;

    [Fact]
    public void Every_interactive_control_in_the_markup_is_named()
    {
        XamlRuleResult result = new InteractiveAutomationNameRule(BeyondTheFrameworkList).Analyze(PackageSources.Markup());

        Assert.True(
            result.Inspected >= MinimumInteractiveControls,
            $"XQ1002 examined {result.Inspected} interactive control(s) under {PackageSources.Project}, "
            + $"expected at least {MinimumInteractiveControls}. The scan has stopped seeing the controls, "
            + "so a clean result would mean nothing.");

        Assert.True(
            result.Findings.Count == 0,
            $"{result.Findings.Count} of {result.Inspected} interactive control(s) under "
            + $"{PackageSources.Project} declare no automation name:\n  "
            + string.Join("\n  ", result.Findings)
            + "\n\nGive each one AutomationProperties.Name. Fixed chrome takes a WrapperStrings value "
            + "({x:Static loc:WrapperStrings.…}), so a host's Resolver localises it with the rest; an "
            + "editor input binds the view-model's DisplayName. Where the visible label is itself a good "
            + "announcement, reuse the label's own string.");
    }

    /// <summary>
    /// Guards the SCAN, not the markup. A guard that silently stops looking is worse than no guard:
    /// the suite stays green and the gap is invisible.
    /// </summary>
    /// <remarks>
    /// ⚠ The original asserted a repository-wide scan, because its root had once been hardcoded to one
    /// app's views folder and every other UI project went unguarded. This scan is narrower on purpose,
    /// one project, and that is sound only while that project holds all the markup there is. So the
    /// last assertion looks at the whole of <c>src/</c>.
    /// </remarks>
    [Fact]
    public void The_scan_covers_all_the_markup_not_one_hardcoded_folder()
    {
        XamlScanContext markup = PackageSources.Markup();

        HashSet<string> reached = new(
            markup.Files.Select(f => f.RelativePath.Contains('/')
                ? f.RelativePath[..f.RelativePath.LastIndexOf('/')]
                : string.Empty),
            StringComparer.Ordinal);

        Assert.True(
            reached.Count > 1,
            $"The markup scan found files in only {reached.Count} directory of {PackageSources.Project}. "
            + "A single-directory result is the hardcoded-folder regression this test exists for.");

        string[] missed = [.. DirectoriesThatMustContributeMarkup.Where(d => !reached.Contains(d))];

        Assert.True(
            missed.Length == 0,
            $"The markup scan found no files in: {string.Join(", ", missed)}. It reached only: "
            + $"{string.Join(", ", reached.Order(StringComparer.Ordinal))}. Either the scan narrowed, or a "
            + "directory was legitimately moved or renamed, in which case update "
            + "DirectoriesThatMustContributeMarkup in the same commit.");

        string[] outside =
        [
            .. XamlScanContext.Load(Path.Combine(PackageSources.RepoRoot(), "src")).Files
                .Select(f => "src/" + f.RelativePath)
                .Where(p => !p.StartsWith(PackageSources.Project + "/", StringComparison.Ordinal)),
        ];

        Assert.True(
            outside.Length == 0,
            $"Markup outside {PackageSources.Project}, which no guard here reads:\n  "
            + string.Join("\n  ", outside)
            + "\n\nWiden PackageSources to cover it. A project that holds markup and is not scanned is "
            + "exactly the gap the original guard was rewritten to close.");
    }
}
