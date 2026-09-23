namespace Bennewitz.Ninja.ScopedEditors.AvaloniaUI.Tests.ViewModels;

public class StringArrayPropertyEditorViewModelTests
{
    private static FakeEditorSchema Schema()
    {
        return new FakeEditorSchema("settings.tags", EditorValueType.StringArray);
    }

    private static FakeEditorValue Empty()
    {
        return new FakeEditorValue("settings.tags");
    }

    private static FakeEditorValue WithUser(IReadOnlyList<object?> items)
    {
        return new FakeEditorValue("settings.tags").With(FakeEditorScope.User, items);
    }

    // ── Construction ──────────────────────────────────────────────────────────

    [Fact]
    public void Initial_Items_Empty_NotModified()
    {
        StringArrayPropertyEditorViewModel vm = new(Schema(), FakeEditorScope.User);
        Assert.Empty(vm.Items);
        Assert.False(vm.IsModified);
        Assert.False(vm.IsValueSet);
    }

    // ── LoadFromValue ─────────────────────────────────────────────────────────

    [Fact]
    public void LoadFromValue_Empty_ItemsRemainEmpty()
    {
        StringArrayPropertyEditorViewModel vm = new(Schema(), FakeEditorScope.User);
        vm.LoadFromValue(Empty(), FakeEditorScope.User);

        Assert.Empty(vm.Items);
        Assert.False(vm.IsModified);
    }

    [Fact]
    public void LoadFromValue_SetsItems()
    {
        FakeEditorValue value = WithUser(["alpha", "beta", "gamma"]);
        StringArrayPropertyEditorViewModel vm = new(Schema(), FakeEditorScope.User);
        vm.LoadFromValue(value, FakeEditorScope.User);

        Assert.Equal(3, vm.Items.Count);
        Assert.Equal("alpha", vm.Items[0]);
        Assert.Equal("beta", vm.Items[1]);
        Assert.Equal("gamma", vm.Items[2]);
        Assert.True(vm.IsModified);
        Assert.True(vm.IsValueSet);
    }

    [Fact]
    public void LoadFromValue_OtherScope_NoItems()
    {
        FakeEditorValue value = new FakeEditorValue("settings.tags")
            .With(FakeEditorScope.Project, (IReadOnlyList<object?>)["x"]);

        StringArrayPropertyEditorViewModel vm = new(Schema(), FakeEditorScope.User);
        vm.LoadFromValue(value, FakeEditorScope.User);

        Assert.Empty(vm.Items);
    }

    [Fact]
    public void LoadFromValue_MixedTypeArray_CoercesToString()
    {
        // Non-string items are ToString()'d
        FakeEditorValue value = new FakeEditorValue("settings.tags")
            .With(FakeEditorScope.User, (IReadOnlyList<object?>)["text", 42, true]);

        StringArrayPropertyEditorViewModel vm = new(Schema(), FakeEditorScope.User);
        vm.LoadFromValue(value, FakeEditorScope.User);

        Assert.Equal(3, vm.Items.Count);
        Assert.Equal("text", vm.Items[0]);
        Assert.Equal("42", vm.Items[1]);
        Assert.Equal("True", vm.Items[2]);
    }

    // ── ToValue ───────────────────────────────────────────────────────────────

    [Fact]
    public void ToValue_ReturnsNull_WhenEmpty()
    {
        StringArrayPropertyEditorViewModel vm = new(Schema(), FakeEditorScope.User);
        Assert.Null(vm.ToValue());
    }

    [Fact]
    public void ToValue_ReturnsList_WhenItemsExist()
    {
        StringArrayPropertyEditorViewModel vm = new(Schema(), FakeEditorScope.User);
        vm.Items.Add("one");
        vm.Items.Add("two");

        IReadOnlyList<object?>? result = vm.ToValue() as IReadOnlyList<object?>;
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Equal("one", result[0]);
        Assert.Equal("two", result[1]);
    }

    // ── Add/Remove commands ───────────────────────────────────────────────────

    [Fact]
    public void AddItem_AddsToList_AndClearsText()
    {
        StringArrayPropertyEditorViewModel vm = new(Schema(), FakeEditorScope.User);
        vm.NewItemText = "hello";
        vm.AddItemCommand.Execute(null);

        Assert.Single(vm.Items);
        Assert.Equal("hello", vm.Items[0]);
        Assert.Equal(string.Empty, vm.NewItemText);
    }

    [Fact]
    public void AddItem_NoDuplicates()
    {
        StringArrayPropertyEditorViewModel vm = new(Schema(), FakeEditorScope.User);
        vm.NewItemText = "dup";
        vm.AddItemCommand.Execute(null);
        vm.NewItemText = "dup";
        vm.AddItemCommand.Execute(null);

        Assert.Single(vm.Items);
    }

    [Fact]
    public void AddItemCommand_CannotExecute_WhenTextEmpty()
    {
        StringArrayPropertyEditorViewModel vm = new(Schema(), FakeEditorScope.User);
        vm.NewItemText = "";
        Assert.False(vm.AddItemCommand.CanExecute(null));
    }

    [Fact]
    public void RemoveItem_RemovesFromList()
    {
        StringArrayPropertyEditorViewModel vm = new(Schema(), FakeEditorScope.User);
        vm.Items.Add("a");
        vm.Items.Add("b");
        vm.RemoveItemCommand.Execute("a");

        Assert.Single(vm.Items);
        Assert.Equal("b", vm.Items[0]);
    }

    // ── Reset ─────────────────────────────────────────────────────────────────

    [Fact]
    public void ResetToInherited_ClearsItems()
    {
        StringArrayPropertyEditorViewModel vm = new(Schema(), FakeEditorScope.User);
        vm.Items.Add("item1");
        vm.Items.Add("item2");

        vm.ResetToInheritedCommand.Execute(null);

        Assert.Empty(vm.Items);
        Assert.False(vm.IsModified);
    }

    // ── OtherScopesWithData + InheritedFromScope ──────────────────

    [Fact]
    public void LoadFromValue_OtherScopesWithData_PopulatedFromDefiningScopes()
    {
        FakeEditorValue value = new FakeEditorValue("settings.tags")
                                .With(FakeEditorScope.User, new object?[] { "a", "b" })
                                .With(FakeEditorScope.Project, new object?[] { "c" });
        StringArrayPropertyEditorViewModel vm = new(Schema(), FakeEditorScope.User);
        vm.LoadFromValue(value, FakeEditorScope.User);

        Assert.Single(vm.OtherScopesWithData);
        Assert.Equal("project", vm.OtherScopesWithData[0].Id);
    }
}