namespace Bennewitz.Ninja.ScopedEditors.AvaloniaUI.Tests.ViewModels;

public class StringPropertyEditorViewModelTests
{
    private static FakeEditorSchema Schema()
    {
        return new FakeEditorSchema("settings.name");
    }

    private static FakeEditorValue Empty()
    {
        return new FakeEditorValue("settings.name");
    }

    private static FakeEditorValue WithUser(string v)
    {
        return new FakeEditorValue("settings.name").With(FakeEditorScope.User, v);
    }

    // ── Construction ──────────────────────────────────────────────────────────

    [Fact]
    public void Initial_Value_IsNull_NotModified()
    {
        StringPropertyEditorViewModel vm = new(Schema(), FakeEditorScope.User);
        Assert.Null(vm.Value);
        Assert.False(vm.IsModified);
        Assert.False(vm.IsValueSet);
    }

    // ── LoadFromValue ─────────────────────────────────────────────────────────

    [Fact]
    public void LoadFromValue_Empty_ValueStaysNull()
    {
        StringPropertyEditorViewModel vm = new(Schema(), FakeEditorScope.User);
        vm.LoadFromValue(Empty(), FakeEditorScope.User);

        Assert.Null(vm.Value);
        Assert.False(vm.IsModified);
    }

    [Fact]
    public void LoadFromValue_SetsValue()
    {
        StringPropertyEditorViewModel vm = new(Schema(), FakeEditorScope.User);
        vm.LoadFromValue(WithUser("hello"), FakeEditorScope.User);

        Assert.Equal("hello", vm.Value);
        Assert.True(vm.IsModified);
        Assert.True(vm.IsValueSet);
    }

    [Fact]
    public void LoadFromValue_OtherScope_NoValueAtEditingScope()
    {
        FakeEditorValue value = new FakeEditorValue("settings.name").With(FakeEditorScope.Project, "project-value");

        StringPropertyEditorViewModel vm = new(Schema(), FakeEditorScope.User);
        vm.LoadFromValue(value, FakeEditorScope.User);

        Assert.Null(vm.Value);
        Assert.False(vm.IsModified);
    }

    // ── ToValue ───────────────────────────────────────────────────────────────

    [Fact]
    public void ToValue_ReturnsNull_WhenNotSet()
    {
        StringPropertyEditorViewModel vm = new(Schema(), FakeEditorScope.User);
        Assert.Null(vm.ToValue());
    }

    [Fact]
    public void ToValue_ReturnsString_WhenSet()
    {
        StringPropertyEditorViewModel vm = new(Schema(), FakeEditorScope.User);
        vm.Value = "world";
        Assert.Equal("world", vm.ToValue());
    }

    // ── Reset ─────────────────────────────────────────────────────────────────

    [Fact]
    public void ResetToInherited_ClearsValue()
    {
        StringPropertyEditorViewModel vm = new(Schema(), FakeEditorScope.User);
        vm.Value = "something";

        vm.ResetToInheritedCommand.Execute(null);

        Assert.Null(vm.Value);
        Assert.False(vm.IsModified);
    }

    [Fact]
    public void CanReset_IsTrue_WhenModified()
    {
        StringPropertyEditorViewModel vm = new(Schema(), FakeEditorScope.User);
        vm.Value = "x";
        Assert.True(vm.CanReset);
    }

    // ── OtherScopesWithData + InheritedFromScope ──────────────────

    [Fact]
    public void LoadFromValue_OtherScopesWithData_ListsOtherDefiningScopes()
    {
        FakeEditorValue value = new FakeEditorValue("settings.name")
                                .With(FakeEditorScope.User, "from-user")
                                .With(FakeEditorScope.Project, "from-project");
        StringPropertyEditorViewModel vm = new(Schema(), FakeEditorScope.User);
        vm.LoadFromValue(value, FakeEditorScope.User);

        Assert.Single(vm.OtherScopesWithData);
        Assert.Equal("project", vm.OtherScopesWithData[0].Id);
    }

    [Fact]
    public void LoadFromValue_InheritedFromScope_PopulatedFromOtherScope()
    {
        // Editing at Local, value at User → "Currently effective from User"
        FakeEditorValue value = new FakeEditorValue("settings.name").With(FakeEditorScope.User, "opus");
        StringPropertyEditorViewModel vm = new(Schema(), FakeEditorScope.Local);
        vm.LoadFromValue(value, FakeEditorScope.Local);

        Assert.Null(vm.Value);
        Assert.Equal("user", vm.InheritedFromScope?.Id);
        Assert.Equal("opus", vm.InheritedDisplay);
        Assert.True(vm.HasInheritedFromOtherScope);
    }

    [Fact]
    public void LoadFromValue_LongInheritedValue_TruncatedAt50Chars()
    {
        // The 50-char cap on inherited-value display.
        string longString = new('x', 60);
        FakeEditorValue value = new FakeEditorValue("settings.name").With(FakeEditorScope.User, longString);
        StringPropertyEditorViewModel vm = new(Schema(), FakeEditorScope.Local);
        vm.LoadFromValue(value, FakeEditorScope.Local);

        Assert.NotNull(vm.InheritedDisplay);
        // 50 chars + ellipsis = 51 chars total.
        MessageAssert.Equal(51, vm.InheritedDisplay!.Length, "Inherited display values must be truncated at 50 chars + ellipsis.");
        Assert.EndsWith("…", vm.InheritedDisplay);
    }

    // ── Empty-effective-value display bug ────────────────────────

    [Fact]
    public void LoadFromValue_EmptyEffectiveValue_NoSchemaDefault_FallsThroughToNotSet()
    {
        // Regression for the "(inherits: )" watermark bug.  An AutoCompleteBox
        // TwoWay binding can transiently push an empty string to the
        // underlying scope (e.g. mid-dropdown-interaction, the Text field
        // clears before the new selection is assigned).  When the user then
        // switches editing scope, the new scope's editor sees EffectiveValue=""
        // from the other scope.
        //
        // Pre-fix: InheritedDisplay = "" (TruncateDisplay("") returns ""),
        //          HasInheritedFromOtherScope = true (empty-string is not null),
        //          Watermark = "(inherits: )" with empty value after the colon,
        //          and the chiclet row below the editor rendered with no value
        //          text alongside it.
        // Post-fix: empty formatted value falls through to schema default;
        //          when there is no schema default, both InheritedDisplay and
        //          InheritedFromScope land at null and the watermark says
        //          "(not set)".
        FakeEditorValue value = new FakeEditorValue("settings.name").With(FakeEditorScope.Project, "");
        StringPropertyEditorViewModel vm = new(Schema(), FakeEditorScope.User);
        vm.LoadFromValue(value, FakeEditorScope.User);

        MessageAssert.Null(vm.InheritedDisplay, "Empty-string effective value must collapse to null InheritedDisplay, " +
            "not the literal empty string — otherwise the watermark renders " +
            "'(inherits: )' with an empty value after the colon.");
        MessageAssert.Null(vm.InheritedFromScope, "InheritedFromScope must clear in lockstep with InheritedDisplay so " +
            "HasInheritedFromOtherScope returns false and the chiclet row below " +
            "the editor hides.");
        Assert.False(vm.HasInheritedFromOtherScope);
        Assert.Equal("(not set)", vm.Watermark);
    }

    [Fact]
    public void LoadFromValue_EmptyEffectiveValue_WithSchemaDefault_FallsThroughToDefault()
    {
        // Same bug shape, but the schema has a default — the editor should
        // surface the schema default in the watermark rather than letting the
        // empty-string effective value masquerade as the inherited display.
        FakeEditorSchema schemaWithDefault = new("settings.name")
        {
            DefaultValue = "sonnet",
        };
        FakeEditorValue value = new FakeEditorValue("settings.name").With(FakeEditorScope.Project, "");
        StringPropertyEditorViewModel vm = new(schemaWithDefault, FakeEditorScope.User);
        vm.LoadFromValue(value, FakeEditorScope.User);

        MessageAssert.Equal("sonnet", vm.InheritedDisplay, "Empty effective value must yield to the schema default for display.");
        MessageAssert.Null(vm.InheritedFromScope, "Schema-default fallback has no owning scope — the chiclet row stays hidden.");
        Assert.False(vm.HasInheritedFromOtherScope, "Schema-default inheritance is communicated by the watermark only, " +
            "not by the chiclet row.");
        Assert.Equal("(inherits: sonnet)", vm.Watermark);
    }
}