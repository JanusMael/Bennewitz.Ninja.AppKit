namespace Bennewitz.Ninja.ScopedEditors.AvaloniaUI.Tests.ViewModels;

public class BooleanPropertyEditorViewModelTests
{
    private static FakeEditorSchema Schema()
    {
        return new FakeEditorSchema("settings.flag", EditorValueType.Boolean);
    }

    private static FakeEditorValue Empty()
    {
        return new FakeEditorValue("settings.flag");
    }

    private static FakeEditorValue WithUser(bool v)
    {
        return new FakeEditorValue("settings.flag").With(FakeEditorScope.User, v);
    }

    // ── Construction ──────────────────────────────────────────────────────────

    [Fact]
    public void Initial_Value_IsNull_NotModified()
    {
        BooleanPropertyEditorViewModel vm = new(Schema(), FakeEditorScope.User);
        Assert.Null(vm.Value);
        Assert.False(vm.IsModified);
        Assert.False(vm.IsValueSet);
    }

    // ── LoadFromValue ─────────────────────────────────────────────────────────

    [Fact]
    public void LoadFromValue_Empty_ValueStaysNull()
    {
        BooleanPropertyEditorViewModel vm = new(Schema(), FakeEditorScope.User);
        vm.LoadFromValue(Empty(), FakeEditorScope.User);

        Assert.Null(vm.Value);
        Assert.False(vm.IsModified);
        Assert.Null(vm.EffectiveScope);
    }

    [Fact]
    public void LoadFromValue_SetsTrue()
    {
        BooleanPropertyEditorViewModel vm = new(Schema(), FakeEditorScope.User);
        vm.LoadFromValue(WithUser(true), FakeEditorScope.User);

        Assert.True(vm.Value);
        Assert.True(vm.IsModified);
        Assert.True(vm.IsValueSet);
        Assert.Equal("user", vm.EffectiveScope?.Id);
    }

    [Fact]
    public void LoadFromValue_SetsFalse()
    {
        BooleanPropertyEditorViewModel vm = new(Schema(), FakeEditorScope.User);
        vm.LoadFromValue(WithUser(false), FakeEditorScope.User);

        Assert.False(vm.Value);
        Assert.True(vm.IsModified);
    }

    [Fact]
    public void LoadFromValue_OtherScope_NoValueAtEditingScope()
    {
        // Value at Project, editing User → no value at User
        FakeEditorValue value = new FakeEditorValue("settings.flag")
            .With(FakeEditorScope.Project, true);

        BooleanPropertyEditorViewModel vm = new(Schema(), FakeEditorScope.User);
        vm.LoadFromValue(value, FakeEditorScope.User);

        Assert.Null(vm.Value);
        Assert.False(vm.IsModified);
    }

    [Fact]
    public void LoadFromValue_IsOverridden_WhenMultipleScopes()
    {
        FakeEditorValue value = new FakeEditorValue("settings.flag")
                                .With(FakeEditorScope.User, true)
                                .With(FakeEditorScope.Project, false);

        BooleanPropertyEditorViewModel vm = new(Schema(), FakeEditorScope.User);
        vm.LoadFromValue(value, FakeEditorScope.User);

        Assert.True(vm.IsOverridden);
    }

    // ── ToValue ───────────────────────────────────────────────────────────────

    [Fact]
    public void ToValue_ReturnsNull_WhenNotSet()
    {
        BooleanPropertyEditorViewModel vm = new(Schema(), FakeEditorScope.User);
        Assert.Null(vm.ToValue());
    }

    [Fact]
    public void ToValue_ReturnsBool_WhenSet()
    {
        BooleanPropertyEditorViewModel vm = new(Schema(), FakeEditorScope.User);
        vm.Value = false;
        Assert.False((bool?)vm.ToValue());
    }

    // ── Reset ─────────────────────────────────────────────────────────────────

    [Fact]
    public void ResetToInherited_ClearsValue()
    {
        BooleanPropertyEditorViewModel vm = new(Schema(), FakeEditorScope.User);
        vm.Value = true;

        vm.ResetToInheritedCommand.Execute(null);

        Assert.Null(vm.Value);
        Assert.False(vm.IsModified);
    }

    [Fact]
    public void CanReset_IsFalse_WhenNotModified()
    {
        BooleanPropertyEditorViewModel vm = new(Schema(), FakeEditorScope.User);
        Assert.False(vm.CanReset);
    }

    [Fact]
    public void CanReset_IsTrue_WhenModified()
    {
        BooleanPropertyEditorViewModel vm = new(Schema(), FakeEditorScope.User);
        vm.Value = true;
        Assert.True(vm.CanReset);
    }

    [Fact]
    public void CanReset_IsFalse_WhenReadOnlyScope()
    {
        BooleanPropertyEditorViewModel vm = new(Schema(), FakeEditorScope.Managed);
        vm.LoadFromValue(
            new FakeEditorValue("settings.flag").With(FakeEditorScope.Managed, true),
            FakeEditorScope.Managed);
        // Managed scope is read-only → CanReset must be false
        Assert.False(vm.CanReset);
    }

    // ── OtherScopesWithData (chiclets row) + InheritedFromScope ─────

    [Fact]
    public void LoadFromValue_OtherScopesWithData_ExcludesEditingScope_IncludesOthers()
    {
        // Value defined at both User and Project; editing at User.  The
        // "Defined in scopes:" wrapper row should list Project only (the
        // editing scope's own data is implicit in the editor itself).
        FakeEditorValue value = new FakeEditorValue("settings.flag")
                                .With(FakeEditorScope.User, true)
                                .With(FakeEditorScope.Project, false);

        BooleanPropertyEditorViewModel vm = new(Schema(), FakeEditorScope.User);
        vm.LoadFromValue(value, FakeEditorScope.User);

        Assert.Single(vm.OtherScopesWithData);
        Assert.Equal("project", vm.OtherScopesWithData[0].Id);
    }

    [Fact]
    public void LoadFromValue_OtherScopesWithData_OnlyEditingScope_IsEmpty()
    {
        // Value defined only at the editing scope → no OTHER scope has data.
        FakeEditorValue value = new FakeEditorValue("settings.flag").With(FakeEditorScope.User, true);
        BooleanPropertyEditorViewModel vm = new(Schema(), FakeEditorScope.User);
        vm.LoadFromValue(value, FakeEditorScope.User);

        MessageAssert.Equal(0, vm.OtherScopesWithData.Count, "When only the editing scope has data, OtherScopesWithData must be empty.");
    }

    [Fact]
    public void LoadFromValue_InheritedFromScope_PopulatedWhenEditingScopeEmpty()
    {
        // Editing at Local; only User has data.  Editor is empty at Local
        // → "Currently effective from User: …" row should fire.
        FakeEditorValue value = new FakeEditorValue("settings.flag")
            .With(FakeEditorScope.User, true);
        BooleanPropertyEditorViewModel vm = new(Schema(), FakeEditorScope.Local);
        vm.LoadFromValue(value, FakeEditorScope.Local);

        MessageAssert.Null(vm.Value, "Editor at empty scope shows null.");
        MessageAssert.NotNull(vm.InheritedFromScope, "InheritedFromScope must be set when the editing scope is empty " +
            "but another scope owns the value.");
        Assert.Equal("user", vm.InheritedFromScope!.Id);
        Assert.Equal("true", vm.InheritedDisplay);
        Assert.True(vm.HasInheritedFromOtherScope, "Wrapper-row visibility flag must fire when both display + scope are set.");
    }

    [Fact]
    public void LoadFromValue_InheritedFromScope_NullWhenEditingScopeOwnsValue()
    {
        // When the editing scope itself has the value, there's nothing to
        // inherit — the editor IS the source of truth.
        FakeEditorValue value = new FakeEditorValue("settings.flag").With(FakeEditorScope.User, true);
        BooleanPropertyEditorViewModel vm = new(Schema(), FakeEditorScope.User);
        vm.LoadFromValue(value, FakeEditorScope.User);

        Assert.Null(vm.InheritedFromScope);
        Assert.Null(vm.InheritedDisplay);
        Assert.False(vm.HasInheritedFromOtherScope);
    }
}