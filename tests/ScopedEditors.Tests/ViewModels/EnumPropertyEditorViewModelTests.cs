using System.Collections;

namespace Bennewitz.Ninja.ScopedEditors.AvaloniaUI.Tests.ViewModels;

public class EnumPropertyEditorViewModelTests
{
    private static readonly IReadOnlyList<string> Options = ["red", "green", "blue"];

    private static FakeEditorSchema Schema()
    {
        return new FakeEditorSchema("settings.color", EditorValueType.Enum) { EnumValues = Options };
    }

    private static FakeEditorValue Empty()
    {
        return new FakeEditorValue("settings.color");
    }

    private static FakeEditorValue WithUser(string v)
    {
        return new FakeEditorValue("settings.color").With(FakeEditorScope.User, v);
    }

    // ── Construction ──────────────────────────────────────────────────────────

    [Fact]
    public void EnumOptions_PopulatedFromSchema()
    {
        EnumPropertyEditorViewModel vm = new(Schema(), FakeEditorScope.User);
        Assert.Equal((ICollection)Options, (ICollection)vm.EnumOptions);
    }

    [Fact]
    public void EnumOptionItems_PairValuesWithDescriptions_NullWhenAbsent()
    {
        // EnumOptionItems pairs each value with its per-value description (for the
        // per-item tooltip); a value with no description maps to a null tooltip.
        FakeEditorSchema schema = new("settings.color", EditorValueType.Enum)
        {
            EnumValues = Options, // red, green, blue
            EnumValueDescriptions = new Dictionary<string, string>
            {
                ["red"] = "warm",
                ["blue"] = "cool",
                // "green" intentionally omitted.
            },
        };
        EnumPropertyEditorViewModel vm = new(schema, FakeEditorScope.User);

        Assert.Equal(3, vm.EnumOptionItems.Count);
        Assert.Equal("red", vm.EnumOptionItems[0].Value);
        Assert.Equal("warm", vm.EnumOptionItems[0].Description);
        Assert.Equal("green", vm.EnumOptionItems[1].Value);
        MessageAssert.Null(vm.EnumOptionItems[1].Description, "A value with no description maps to a null tooltip.");
        Assert.Equal("blue", vm.EnumOptionItems[2].Value);
        Assert.Equal("cool", vm.EnumOptionItems[2].Description);
    }

    [Fact]
    public void EnumOptionItems_AllNullDescriptions_WhenSchemaHasNone()
    {
        EnumPropertyEditorViewModel vm = new(Schema(), FakeEditorScope.User);
        Assert.Equal(3, vm.EnumOptionItems.Count);
        foreach (EnumOption item in vm.EnumOptionItems)
        {
            MessageAssert.Null(item.Description, "No descriptions in the schema → every item has a null tooltip.");
        }
    }

    [Fact]
    public void Initial_SelectedValue_IsNull_NotModified()
    {
        EnumPropertyEditorViewModel vm = new(Schema(), FakeEditorScope.User);
        Assert.Null(vm.SelectedValue);
        Assert.False(vm.IsModified);
        Assert.False(vm.IsValueSet);
    }

    // ── LoadFromValue ─────────────────────────────────────────────────────────

    [Fact]
    public void LoadFromValue_SetsSelectedValue()
    {
        EnumPropertyEditorViewModel vm = new(Schema(), FakeEditorScope.User);
        vm.LoadFromValue(WithUser("green"), FakeEditorScope.User);

        Assert.Equal("green", vm.SelectedValue);
        Assert.True(vm.IsModified);
        Assert.True(vm.IsValueSet);
    }

    [Fact]
    public void LoadFromValue_Empty_SelectedValueIsNull()
    {
        EnumPropertyEditorViewModel vm = new(Schema(), FakeEditorScope.User);
        vm.LoadFromValue(Empty(), FakeEditorScope.User);

        Assert.Null(vm.SelectedValue);
        Assert.False(vm.IsModified);
    }

    [Fact]
    public void LoadFromValue_OtherScope_NoValueAtEditingScope()
    {
        FakeEditorValue value = new FakeEditorValue("settings.color").With(FakeEditorScope.Project, "blue");

        EnumPropertyEditorViewModel vm = new(Schema(), FakeEditorScope.User);
        vm.LoadFromValue(value, FakeEditorScope.User);

        Assert.Null(vm.SelectedValue);
    }

    // ── ToValue ───────────────────────────────────────────────────────────────

    [Fact]
    public void ToValue_ReturnsNull_WhenNotSet()
    {
        EnumPropertyEditorViewModel vm = new(Schema(), FakeEditorScope.User);
        Assert.Null(vm.ToValue());
    }

    [Fact]
    public void ToValue_ReturnsSelectedString()
    {
        EnumPropertyEditorViewModel vm = new(Schema(), FakeEditorScope.User);
        vm.SelectedValue = "red";
        Assert.Equal("red", vm.ToValue());
    }

    // ── Reset ─────────────────────────────────────────────────────────────────

    [Fact]
    public void ResetToInherited_ClearsSelectedValue()
    {
        EnumPropertyEditorViewModel vm = new(Schema(), FakeEditorScope.User);
        vm.SelectedValue = "blue";

        vm.ResetToInheritedCommand.Execute(null);

        Assert.Null(vm.SelectedValue);
        Assert.False(vm.IsModified);
    }

    // ── EnumOptions null schema ───────────────────────────────────────────────

    [Fact]
    public void EnumOptions_EmptyList_WhenSchemaHasNoEnumValues()
    {
        FakeEditorSchema schema = new("settings.x", EditorValueType.Enum); // EnumValues is null
        EnumPropertyEditorViewModel vm = new(schema, FakeEditorScope.User);
        Assert.Empty(vm.EnumOptions);
    }

    // ── Free-form vs strict enum ──────────────────────────────────────────────

    /// <summary>
    /// Enums promoted from the schema <c>examples</c> keyword (AllowsFreeForm=true) must
    /// accept arbitrary values so users can type custom model identifiers etc.
    /// </summary>
    [Fact]
    public void FreeForm_SelectedValue_AcceptsArbitraryString()
    {
        FakeEditorSchema schema = new("settings.model", EditorValueType.Enum)
        {
            EnumValues = ["claude-sonnet-4-5", "claude-opus-4"],
            Examples = ["claude-sonnet-4-5", "claude-opus-4"], // promotes to free-form
        };
        EnumPropertyEditorViewModel vm = new(schema, FakeEditorScope.User);

        Assert.True(vm.AllowsFreeForm, "Schema with examples should be free-form.");
        Assert.False(vm.IsStrictEnum);

        vm.SelectedValue = "my-custom-model-id";
        Assert.Equal("my-custom-model-id", vm.SelectedValue);
        Assert.Equal("my-custom-model-id", vm.ToValue());
    }

    /// <summary>
    /// Strict enums (no <c>examples</c>, AllowsFreeForm=false) with a schema default
    /// should surface the default in the Watermark via <c>(inherits: X)</c> rather
    /// than the fallback <c>(not set)</c>. Pins the fix for Issue F.1.
    /// </summary>
    [Fact]
    public void StrictEnum_WithSchemaDefault_WatermarkShowsInheritsDefault()
    {
        FakeEditorSchema schema = new("settings.effortLevel", EditorValueType.Enum)
        {
            EnumValues = ["low", "medium", "high"],
            DefaultValue = "medium",
        };
        EnumPropertyEditorViewModel vm = new(schema, FakeEditorScope.User);
        vm.LoadFromValue(Empty() /* no value at any scope */, FakeEditorScope.User);

        Assert.False(vm.AllowsFreeForm);
        Assert.Equal("(inherits: medium)", vm.Watermark);
    }

    // ── OtherScopesWithData + InheritedFromScope ──────────────────

    [Fact]
    public void LoadFromValue_OtherScopesWithData_PopulatedFromDefiningScopes()
    {
        FakeEditorValue value = new FakeEditorValue("settings.color")
                                .With(FakeEditorScope.User, "red")
                                .With(FakeEditorScope.Project, "blue");
        EnumPropertyEditorViewModel vm = new(Schema(), FakeEditorScope.User);
        vm.LoadFromValue(value, FakeEditorScope.User);

        Assert.Single(vm.OtherScopesWithData);
        Assert.Equal("project", vm.OtherScopesWithData[0].Id);
    }

    [Fact]
    public void LoadFromValue_InheritedFromScope_PopulatedWhenEditingScopeEmpty()
    {
        FakeEditorValue value = new FakeEditorValue("settings.color").With(FakeEditorScope.User, "red");
        EnumPropertyEditorViewModel vm = new(Schema(), FakeEditorScope.Local);
        vm.LoadFromValue(value, FakeEditorScope.Local);

        Assert.Null(vm.SelectedValue);
        Assert.Equal("user", vm.InheritedFromScope?.Id);
        Assert.Equal("red", vm.InheritedDisplay);
        Assert.True(vm.HasInheritedFromOtherScope);
    }
}