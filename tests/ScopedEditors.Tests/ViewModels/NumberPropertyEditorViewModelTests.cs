namespace Bennewitz.Ninja.ScopedEditors.AvaloniaUI.Tests.ViewModels;

public class NumberPropertyEditorViewModelTests
{
    private static FakeEditorSchema IntSchema()
    {
        return new FakeEditorSchema("settings.timeout", EditorValueType.Integer) { Minimum = 0, Maximum = 3600 };
    }

    private static FakeEditorSchema FloatSchema()
    {
        return new FakeEditorSchema("settings.ratio", EditorValueType.Number);
    }

    private static FakeEditorValue Empty()
    {
        return new FakeEditorValue("settings.timeout");
    }

    // ── Construction ──────────────────────────────────────────────────────────

    [Fact]
    public void IntegerSchema_SetsIsInteger_True()
    {
        NumberPropertyEditorViewModel vm = new(IntSchema(), FakeEditorScope.User);
        Assert.True(vm.IsInteger);
        Assert.Equal(0d, vm.Minimum);
        Assert.Equal(3600d, vm.Maximum);
    }

    [Fact]
    public void FloatSchema_SetsIsInteger_False()
    {
        NumberPropertyEditorViewModel vm = new(FloatSchema(), FakeEditorScope.User);
        Assert.False(vm.IsInteger);
    }

    [Fact]
    public void Initial_Value_IsNull_NotModified()
    {
        NumberPropertyEditorViewModel vm = new(IntSchema(), FakeEditorScope.User);
        Assert.Null(vm.Value);
        Assert.False(vm.IsModified);
    }

    // ── LoadFromValue — various numeric types ─────────────────────────────────

    [Fact]
    public void LoadFromValue_Long()
    {
        FakeEditorValue value = new FakeEditorValue("settings.timeout").With(FakeEditorScope.User, (long)42);
        NumberPropertyEditorViewModel vm = new(IntSchema(), FakeEditorScope.User);
        vm.LoadFromValue(value, FakeEditorScope.User);

        Assert.Equal(42d, vm.Value);
        Assert.True(vm.IsModified);
    }

    [Fact]
    public void LoadFromValue_Int()
    {
        FakeEditorValue value = new FakeEditorValue("settings.timeout").With(FakeEditorScope.User, 100);
        NumberPropertyEditorViewModel vm = new(IntSchema(), FakeEditorScope.User);
        vm.LoadFromValue(value, FakeEditorScope.User);

        Assert.Equal(100d, vm.Value);
        Assert.True(vm.IsModified);
    }

    [Fact]
    public void LoadFromValue_Double()
    {
        FakeEditorValue value = new FakeEditorValue("settings.ratio").With(FakeEditorScope.User, 3.14);
        NumberPropertyEditorViewModel vm = new(FloatSchema(), FakeEditorScope.User);
        vm.LoadFromValue(value, FakeEditorScope.User);

        Assert.Equal(3.14, vm.Value);
    }

    [Fact]
    public void LoadFromValue_Empty_ValueStaysNull()
    {
        NumberPropertyEditorViewModel vm = new(IntSchema(), FakeEditorScope.User);
        vm.LoadFromValue(Empty(), FakeEditorScope.User);

        Assert.Null(vm.Value);
        Assert.False(vm.IsModified);
    }

    // ── ToValue ───────────────────────────────────────────────────────────────

    [Fact]
    public void ToValue_ReturnsNull_WhenNotSet()
    {
        NumberPropertyEditorViewModel vm = new(IntSchema(), FakeEditorScope.User);
        Assert.Null(vm.ToValue());
    }

    [Fact]
    public void ToValue_ReturnsLong_ForIntegerSchema()
    {
        NumberPropertyEditorViewModel vm = new(IntSchema(), FakeEditorScope.User);
        vm.Value = 7.0;
        Assert.Equal((long)7, vm.ToValue());
        Assert.IsAssignableFrom<long>(vm.ToValue());
    }

    [Fact]
    public void ToValue_ReturnsDouble_ForFloatSchema()
    {
        NumberPropertyEditorViewModel vm = new(FloatSchema(), FakeEditorScope.User);
        vm.Value = 2.5;
        Assert.Equal(2.5, vm.ToValue());
        Assert.IsAssignableFrom<double>(vm.ToValue());
    }

    // ── Reset ─────────────────────────────────────────────────────────────────

    [Fact]
    public void ResetToInherited_ClearsValue()
    {
        NumberPropertyEditorViewModel vm = new(IntSchema(), FakeEditorScope.User);
        vm.Value = 99.0;

        vm.ResetToInheritedCommand.Execute(null);

        Assert.Null(vm.Value);
        Assert.False(vm.IsModified);
    }

    // ── OtherScopesWithData + InheritedFromScope ──────────────────

    [Fact]
    public void LoadFromValue_OtherScopesWithData_PopulatedFromDefiningScopes()
    {
        FakeEditorValue value = new FakeEditorValue("settings.timeout")
                                .With(FakeEditorScope.User, (long)60)
                                .With(FakeEditorScope.Project, (long)120);
        NumberPropertyEditorViewModel vm = new(IntSchema(), FakeEditorScope.User);
        vm.LoadFromValue(value, FakeEditorScope.User);

        Assert.Single(vm.OtherScopesWithData);
        Assert.Equal("project", vm.OtherScopesWithData[0].Id);
    }

    [Fact]
    public void LoadFromValue_InheritedFromScope_PopulatedWhenEditingScopeEmpty()
    {
        FakeEditorValue value = new FakeEditorValue("settings.timeout").With(FakeEditorScope.User, (long)42);
        NumberPropertyEditorViewModel vm = new(IntSchema(), FakeEditorScope.Local);
        vm.LoadFromValue(value, FakeEditorScope.Local);

        Assert.Null(vm.Value);
        Assert.Equal("user", vm.InheritedFromScope?.Id);
        Assert.Equal("42", vm.InheritedDisplay);
        Assert.True(vm.HasInheritedFromOtherScope);
    }
}