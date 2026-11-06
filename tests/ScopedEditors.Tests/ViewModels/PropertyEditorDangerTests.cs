using System.ComponentModel;
using Bennewitz.Ninja.ScopedEditors.AvaloniaUI.Tests.Fakes;

namespace Bennewitz.Ninja.ScopedEditors.AvaloniaUI.Tests.ViewModels;

/// <summary>
/// The danger surface on <see cref="PropertyEditorViewModel"/>: what it reports, and — the part a
/// static check cannot prove — that it is RE-RAISED when the value or the scope moves.
/// </summary>
/// <remarks>
/// ⚠ <b>A stale danger banner is worse than none.</b> The assessment is a function of the current
/// value, so if <c>PropertyChanged</c> is not raised the UI keeps a red "this value is unsafe"
/// banner over a value the user has already fixed — and keeps a page looking calm after they break
/// something. Every recompute path therefore has a test that watches the notification, not just
/// the property's return value.
/// </remarks>
public sealed class PropertyEditorDangerTests
{
    /// <summary>A classifier whose answer is fully determined by its inputs, so tests can assert
    /// on what it was HANDED as well as what came back.</summary>
    private sealed class SpyClassifier : IDangerClassifier
    {
        public List<(string Path, string? ScopeId, object? Value)> Calls { get; } = [];

        public Func<string, string?, object?, DangerAssessment> Answer { get; init; } =
            (_, _, _) => DangerAssessment.Unremarkable;

        public IReadOnlyCollection<string> ClassifiedPaths => ["x"];

        public DangerAssessment Classify(string path, IEditorScope? scope, object? currentValue)
        {
            Calls.Add((path, scope?.Id, currentValue));
            return Answer(path, scope?.Id, currentValue);
        }
    }

    /// <summary>Minimal concrete editor: a single string value that reports through the base's
    /// own value-change hook, exactly as the real leaf editors do.</summary>
    private sealed class StubEditor : PropertyEditorViewModel
    {
        private string? _value;

        public StubEditor(IEditorSchema schema, IEditorScope scope) : base(schema, scope) { }

        public void SetValue(string? value)
        {
            _value = value;
            TrackValueSet(!string.IsNullOrEmpty(value));
        }

        public void Reset() => ResetToInherited();

        public override object? ToValue() => _value;

        public override void LoadFromValue(IEditorValue value, IEditorScope editingScope) { }

        protected override void OnResetToInherited() => _value = null;
    }

    private static StubEditor Editor(string path = "share", string scopeId = "Global") =>
        new(new FakeEditorSchema(path), new FakeEditorScope(0, scopeId, scopeId));

    private static DangerAssessment Critical(string why) => new(AppSeverity.Critical, true, why);

    // ── No classifier: the pre-existing behaviour, unchanged ──────────────────

    [Fact]
    public void WithNoClassifierEverythingIsUnremarkable()
    {
        StubEditor e = Editor();

        Assert.Equal(DangerAssessment.Unremarkable, e.Danger);
        Assert.False(e.HasDangerSeverity);
        Assert.False(e.IsDangerNow);
        Assert.Equal(string.Empty, e.DangerAccessibleText);
    }

    // ── What the classifier is handed ─────────────────────────────────────────

    [Fact]
    public void TheClassifierIsHandedThePathTheScopeAndTheCurrentValue()
    {
        SpyClassifier spy = new();
        StubEditor e = Editor(path: "provider.x.options.apiKey", scopeId: "Project");
        e.AttachDangerClassifier(spy);
        e.SetValue("sk-live");

        _ = e.Danger;

        (string path, string? scopeId, object? value) = spy.Calls[^1];
        Assert.Equal("provider.x.options.apiKey", path);
        Assert.Equal("Project", scopeId);
        Assert.Equal("sk-live", value);
    }

    /// <remarks>
    /// ⚠ The UNSAVED value, deliberately. The whole point of a danger indicator is to fire while
    /// the change is still being made — an indicator that waits for a save is an obituary.
    /// </remarks>
    [Fact]
    public void TheValueHandedOverIsTheCurrentEditorStateNotTheLoadedOne()
    {
        SpyClassifier spy = new();
        StubEditor e = Editor();
        e.AttachDangerClassifier(spy);

        e.SetValue("first");
        _ = e.Danger;
        e.SetValue("second");
        _ = e.Danger;

        Assert.Equal("second", spy.Calls[^1].Value);
    }

    // ── Projections ───────────────────────────────────────────────────────────

    [Fact]
    public void TheProjectionsFollowTheAssessment()
    {
        StubEditor e = Editor();
        e.AttachDangerClassifier(new SpyClassifier
        {
            Answer = (_, _, _) => Critical("Runs shell commands unattended."),
        });

        Assert.Equal(AppSeverity.Critical, e.Danger.Severity);
        Assert.True(e.HasDangerSeverity);
        Assert.True(e.IsDangerNow);
        Assert.Equal("Critical: Runs shell commands unattended.", e.DangerAccessibleText);
    }

    /// <summary>
    /// A tier with a SAFE value: the dot shows, the banner does not. This is the case that lets a
    /// user spot a dangerous knob before turning it, so it is worth pinning explicitly.
    /// </summary>
    [Fact]
    public void ACriticalTierWithASafeValueHasSeverityButIsNotDangerNow()
    {
        StubEditor e = Editor();
        e.AttachDangerClassifier(new SpyClassifier
        {
            Answer = (_, _, _) => new DangerAssessment(AppSeverity.Critical, false, "why"),
        });

        Assert.True(e.HasDangerSeverity, "the dot must render for an untouched dangerous knob");
        Assert.False(e.IsDangerNow, "...but the banner must not");
    }

    // ── Reactivity: the part that goes stale silently ─────────────────────────

    [Fact]
    public void ChangingTheValueRaisesDanger()
    {
        StubEditor e = Editor();
        e.AttachDangerClassifier(new SpyClassifier());

        List<string?> raised = Watch(e);
        e.SetValue("anything");

        Assert.Contains(nameof(PropertyEditorViewModel.Danger), raised);
        Assert.Contains(nameof(PropertyEditorViewModel.IsDangerNow), raised);
        Assert.Contains(nameof(PropertyEditorViewModel.HasDangerSeverity), raised);
        Assert.Contains(nameof(PropertyEditorViewModel.DangerAccessibleText), raised);
    }

    /// <remarks>
    /// ⛔ The regression this pins: the recompute sits next to an <c>IsModified</c> re-raise that
    /// is guarded by <c>wasModified &amp;&amp; isSet</c>. Reusing that guard would skip the
    /// recompute when swapping one non-empty value for another — the exact case where an unsafe
    /// value becomes safe, leaving the banner up.
    /// </remarks>
    [Fact]
    public void SwappingOneNonEmptyValueForAnotherStillRaisesDanger()
    {
        StubEditor e = Editor();
        e.AttachDangerClassifier(new SpyClassifier());
        e.SetValue("auto");

        List<string?> raised = Watch(e);
        e.SetValue("manual");

        MessageAssert.Contains(nameof(PropertyEditorViewModel.Danger), raised, "IsModified does not move here, but the assessment does");
    }

    [Fact]
    public void ChangingTheEditingScopeRaisesDanger()
    {
        StubEditor e = Editor();
        e.AttachDangerClassifier(new SpyClassifier());

        List<string?> raised = Watch(e);
        e.EditingScope = new FakeEditorScope(0, "Project", "Project");

        MessageAssert.Contains(nameof(PropertyEditorViewModel.Danger), raised, "severity escalates with scope, so the row must repaint even though the value stood still");
    }

    [Fact]
    public void ResettingToInheritedRaisesDanger()
    {
        StubEditor e = Editor();
        e.AttachDangerClassifier(new SpyClassifier());
        e.SetValue("auto");

        List<string?> raised = Watch(e);
        e.Reset();

        Assert.Contains(nameof(PropertyEditorViewModel.Danger), raised);
    }

    [Fact]
    public void AttachingTheClassifierRaisesDangerImmediately()
    {
        StubEditor e = Editor();

        List<string?> raised = Watch(e);
        e.AttachDangerClassifier(new SpyClassifier());

        MessageAssert.Contains(nameof(PropertyEditorViewModel.Danger), raised, "editors are built before the classifier is attached, so the first paint depends on this");
    }

    // ── Set-once ──────────────────────────────────────────────────────────────

    [Fact]
    public void AttachReturnsTheSameInstanceSoAFactoryCanChainIt()
    {
        StubEditor e = Editor();
        Assert.Same(e, e.AttachDangerClassifier(new SpyClassifier()));
    }

    [Fact]
    public void AttachingTwiceThrowsRatherThanSilentlyReplacingThePolicy()
    {
        StubEditor e = Editor();
        e.AttachDangerClassifier(new SpyClassifier());

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => e.AttachDangerClassifier(new SpyClassifier()));

        MessageAssert.Contains("share", ex.Message, "the message must name the path so the offending editor is findable");
    }

    [Fact]
    public void AttachingNullIsAllowedAndLeavesTheEditorUnremarkable()
    {
        StubEditor e = Editor();
        e.AttachDangerClassifier(null);

        Assert.Null(e.DangerClassifier);
        Assert.Equal(DangerAssessment.Unremarkable, e.Danger);

        // Still "unset", so a real classifier can follow — a product with no table must not
        // poison the slot.
        e.AttachDangerClassifier(new SpyClassifier());
        Assert.NotNull(e.DangerClassifier);
    }

    private static List<string?> Watch(INotifyPropertyChanged target)
    {
        List<string?> raised = [];
        target.PropertyChanged += (_, e) => raised.Add(e.PropertyName);
        return raised;
    }
}
