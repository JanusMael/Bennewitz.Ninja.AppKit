namespace Bennewitz.Ninja.ScopedEditors.AvaloniaUI.Tests.ViewModels;

public class PathPropertyEditorViewModelTests
{
    private static FakeEditorSchema Schema()
    {
        return new FakeEditorSchema("settings.configPath", EditorValueType.Path);
    }

    private static FakeEditorValue Empty()
    {
        return new FakeEditorValue("settings.configPath");
    }

    private static FakeEditorValue WithUser(string v)
    {
        return new FakeEditorValue("settings.configPath").With(FakeEditorScope.User, v);
    }

    // ── Construction ──────────────────────────────────────────────────────────

    [Fact]
    public void Initial_Value_IsNull_NotModified()
    {
        PathPropertyEditorViewModel vm = new(Schema(), FakeEditorScope.User);
        Assert.Null(vm.Value);
        Assert.False(vm.IsModified);
        Assert.False(vm.IsValueSet);
    }

    // ── LoadFromValue ─────────────────────────────────────────────────────────

    [Fact]
    public void LoadFromValue_SetsPath()
    {
        PathPropertyEditorViewModel vm = new(Schema(), FakeEditorScope.User);
        vm.LoadFromValue(WithUser("/home/user/.config"), FakeEditorScope.User);

        Assert.Equal("/home/user/.config", vm.Value);
        Assert.True(vm.IsModified);
        Assert.True(vm.IsValueSet);
    }

    [Fact]
    public void LoadFromValue_Empty_ValueStaysNull()
    {
        PathPropertyEditorViewModel vm = new(Schema(), FakeEditorScope.User);
        vm.LoadFromValue(Empty(), FakeEditorScope.User);

        Assert.Null(vm.Value);
        Assert.False(vm.IsModified);
    }

    // ── ToValue ───────────────────────────────────────────────────────────────

    [Fact]
    public void ToValue_ReturnsNull_WhenNotSet()
    {
        PathPropertyEditorViewModel vm = new(Schema(), FakeEditorScope.User);
        Assert.Null(vm.ToValue());
    }

    [Fact]
    public void ToValue_ReturnsPath_WhenSet()
    {
        PathPropertyEditorViewModel vm = new(Schema(), FakeEditorScope.User);
        vm.Value = "/usr/local/bin";
        Assert.Equal("/usr/local/bin", vm.ToValue());
    }

    // ── Browse callback ───────────────────────────────────────────────────────

    [Fact]
    public async Task BrowseCommand_SetsValue_WhenDialogReturnsPath()
    {
        PathPropertyEditorViewModel vm = new(Schema(), FakeEditorScope.User,
            browseDialog: () => Task.FromResult<string?>("/picked/path"));

        await vm.BrowseCommand.ExecuteAsync(null);

        Assert.Equal("/picked/path", vm.Value);
    }

    [Fact]
    public async Task BrowseCommand_DoesNotSetValue_WhenDialogReturnsCancelled()
    {
        PathPropertyEditorViewModel vm = new(Schema(), FakeEditorScope.User,
            browseDialog: () => Task.FromResult<string?>(null));
        vm.Value = "/original";

        await vm.BrowseCommand.ExecuteAsync(null);

        Assert.Equal("/original", vm.Value);
    }

    // ── Reset ─────────────────────────────────────────────────────────────────

    [Fact]
    public void ResetToInherited_ClearsValue()
    {
        PathPropertyEditorViewModel vm = new(Schema(), FakeEditorScope.User);
        vm.Value = "/some/path";

        vm.ResetToInheritedCommand.Execute(null);

        Assert.Null(vm.Value);
        Assert.False(vm.IsModified);
    }

    // ── OtherScopesWithData + InheritedFromScope ──────────────────

    [Fact]
    public void LoadFromValue_OtherScopesWithData_PopulatedFromDefiningScopes()
    {
        FakeEditorValue value = new FakeEditorValue("settings.configPath")
                                .With(FakeEditorScope.User, "/home/user/config")
                                .With(FakeEditorScope.Project, "./project-config");
        PathPropertyEditorViewModel vm = new(Schema(), FakeEditorScope.User);
        vm.LoadFromValue(value, FakeEditorScope.User);

        Assert.Single(vm.OtherScopesWithData);
        Assert.Equal("project", vm.OtherScopesWithData[0].Id);
    }

    [Fact]
    public void LoadFromValue_InheritedFromScope_PopulatedWhenEditingScopeEmpty()
    {
        FakeEditorValue value = new FakeEditorValue("settings.configPath").With(FakeEditorScope.User, "/home/user/config");
        PathPropertyEditorViewModel vm = new(Schema(), FakeEditorScope.Local);
        vm.LoadFromValue(value, FakeEditorScope.Local);

        Assert.Null(vm.Value);
        Assert.Equal("user", vm.InheritedFromScope?.Id);
        Assert.Equal("/home/user/config", vm.InheritedDisplay);
        Assert.True(vm.HasInheritedFromOtherScope);
    }
}