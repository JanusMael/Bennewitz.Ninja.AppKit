namespace Bennewitz.Ninja.ScopedEditors.AvaloniaUI.Tests.ViewModels;

public class DefaultPropertyEditorFactoryTests
{
    private readonly DefaultPropertyEditorFactory _factory = new();

    private static IEditorScope Scope => FakeEditorScope.User;

    private static FakeEditorSchema Schema(EditorValueType t, string name = "prop")
    {
        return new FakeEditorSchema($"root.{name}", t) { EnumValues = t == EditorValueType.Enum ? ["a", "b"] : null };
    }

    // ── Basic dispatch ─────────────────────────────────────────────────────────

    [Fact]
    public void Create_Boolean_ReturnsBooleanVM()
    {
        PropertyEditorViewModel vm = _factory.Create(Schema(EditorValueType.Boolean), null, Scope);
        Assert.IsAssignableFrom<BooleanPropertyEditorViewModel>(vm);
    }

    [Fact]
    public void Create_String_ReturnsStringVM()
    {
        PropertyEditorViewModel vm = _factory.Create(Schema(EditorValueType.String), null, Scope);
        Assert.IsAssignableFrom<StringPropertyEditorViewModel>(vm);
    }

    [Fact]
    public void Create_Path_ReturnsPathVM()
    {
        PropertyEditorViewModel vm = _factory.Create(Schema(EditorValueType.Path), null, Scope);
        Assert.IsAssignableFrom<PathPropertyEditorViewModel>(vm);
    }

    [Fact]
    public void Create_Enum_ReturnsEnumVM()
    {
        PropertyEditorViewModel vm = _factory.Create(Schema(EditorValueType.Enum), null, Scope);
        Assert.IsAssignableFrom<EnumPropertyEditorViewModel>(vm);
        Assert.Equal(2, ((EnumPropertyEditorViewModel)vm).EnumOptions.Count);
    }

    [Fact]
    public void Create_Integer_ReturnsNumberVM_IsInteger()
    {
        PropertyEditorViewModel vm = _factory.Create(Schema(EditorValueType.Integer), null, Scope);
        Assert.IsAssignableFrom<NumberPropertyEditorViewModel>(vm);
        Assert.True(((NumberPropertyEditorViewModel)vm).IsInteger);
    }

    [Fact]
    public void Create_Number_ReturnsNumberVM_NotInteger()
    {
        PropertyEditorViewModel vm = _factory.Create(Schema(EditorValueType.Number), null, Scope);
        Assert.IsAssignableFrom<NumberPropertyEditorViewModel>(vm);
        Assert.False(((NumberPropertyEditorViewModel)vm).IsInteger);
    }

    [Fact]
    public void Create_StringArray_ReturnsStringArrayVM()
    {
        PropertyEditorViewModel vm = _factory.Create(Schema(EditorValueType.StringArray), null, Scope);
        Assert.IsAssignableFrom<StringArrayPropertyEditorViewModel>(vm);
    }

    [Fact]
    public void Create_Object_ReturnsObjectVM_WithChildren()
    {
        FakeEditorSchema childSchema = new("root.obj.child");
        FakeEditorSchema parentSchema = new("root.obj", EditorValueType.Object)
        {
            Properties = [childSchema],
        };

        PropertyEditorViewModel vm = _factory.Create(parentSchema, null, Scope);
        Assert.IsAssignableFrom<ObjectPropertyEditorViewModel>(vm);
        Assert.Single(((ObjectPropertyEditorViewModel)vm).Children);
    }

    [Theory]
    [InlineData(EditorValueType.Unknown)]
    [InlineData(EditorValueType.Dictionary)]
    [InlineData(EditorValueType.Complex)]
    public void Create_UnsupportedShape_FallsBackToFlaggedStringVM(EditorValueType type)
    {
        // The factory has no structured editor for these shapes. It still returns a
        // text box (the only generic affordance), but TAGGED so the host shows a
        // warning badge — closing the prior SILENT mis-edit hazard.
        PropertyEditorViewModel vm = _factory.Create(Schema(type), null, Scope);
        Assert.IsAssignableFrom<StringPropertyEditorViewModel>(vm);
        Assert.False(string.IsNullOrEmpty(vm.UnsupportedShapeNotice), "An unstructured shape must be flagged with a notice, not a silent text box.");
        Assert.Equal(DefaultPropertyEditorFactory.NoStructuredEditorNotice, vm.UnsupportedShapeNotice);
    }

    [Fact]
    public void Create_StructuredShape_HasNoUnsupportedNotice()
    {
        PropertyEditorViewModel vm = _factory.Create(Schema(EditorValueType.Boolean), null, Scope);
        MessageAssert.Null(vm.UnsupportedShapeNotice, "A structured shape must not be flagged.");
    }

    // ── CreateForGroup ─────────────────────────────────────────────────────────

    [Fact]
    public void CreateForGroup_CreatesManyEditors()
    {
        IReadOnlyList<IEditorSchema> schemas =
        [
            Schema(EditorValueType.Boolean, "flag"),
            Schema(EditorValueType.String, "label"),
            Schema(EditorValueType.Integer, "count"),
        ];

        IReadOnlyList<PropertyEditorViewModel> vms = _factory.CreateForGroup(schemas, null, Scope);

        Assert.Equal(3, vms.Count);
        Assert.IsAssignableFrom<BooleanPropertyEditorViewModel>(vms[0]);
        Assert.IsAssignableFrom<StringPropertyEditorViewModel>(vms[1]);
        Assert.IsAssignableFrom<NumberPropertyEditorViewModel>(vms[2]);
    }
}

public class CompositePropertyEditorFactoryTests
{
    private static IEditorScope Scope => FakeEditorScope.User;

    // ── Registration and dispatch ─────────────────────────────────────────────

    [Fact]
    public void Register_MatchedSchema_UsesCustomFactory()
    {
        CompositePropertyEditorFactory factory = new();
        factory.Register(
            s => s.Name == "special",
            (s, ws, scope, ctx) => new StringArrayPropertyEditorViewModel(s, scope));

        FakeEditorSchema schema = new("root.special");
        PropertyEditorViewModel vm = factory.Create(schema, null, Scope);

        // Even though the schema says String, the registered factory returned StringArray
        Assert.IsAssignableFrom<StringArrayPropertyEditorViewModel>(vm);
    }

    [Fact]
    public void Register_UnmatchedSchema_FallsThroughToDefault()
    {
        CompositePropertyEditorFactory factory = new();
        factory.Register(s => s.Name == "other", (s, ws, scope, ctx) =>
            new BooleanPropertyEditorViewModel(s, scope));

        FakeEditorSchema schema = new("root.prop");
        PropertyEditorViewModel vm = factory.Create(schema, null, Scope);

        Assert.IsAssignableFrom<StringPropertyEditorViewModel>(vm);
    }

    [Fact]
    public void Register_MultipleMatchers_FirstMatchWins()
    {
        CompositePropertyEditorFactory factory = new();
        factory.Register(
            s => s.ValueType == EditorValueType.String,
            (s, ws, scope, ctx) => new BooleanPropertyEditorViewModel(s, scope)); // first: string→bool
        factory.Register(
            s => s.Name == "label",
            (s, ws, scope, ctx) => new NumberPropertyEditorViewModel(s, scope)); // second: label→number

        FakeEditorSchema schema = new("root.label");
        PropertyEditorViewModel vm = factory.Create(schema, null, Scope);

        // First matcher fires (type = String) → Boolean
        Assert.IsAssignableFrom<BooleanPropertyEditorViewModel>(vm);
    }

    // ── Acceptance test: non-Claude, non-JSON consumer ─────────────────────────
    // Verifies the plan's hard Definition of Done:
    //   "A non-Claude, non-JSON consumer can drive the library end-to-end
    //    — creating editors, reading/writing values through the workspace,
    //    firing ValueChanged — without touching AgentForge.Core or System.Text.Json."

    [Fact]
    public void NonClaudeConsumer_EndToEnd_NoJsonNoDomainTypes()
    {
        // Arrange: a factory with one custom editor registered
        CompositePropertyEditorFactory factory = new();
        factory.Register(
            s => s.Name == "tags",
            (s, ws, scope, ctx) => new StringArrayPropertyEditorViewModel(s, scope));

        // Arrange: a workspace pre-seeded entirely through the interface
        FakeEditorWorkspace ws = new([FakeEditorScope.User, FakeEditorScope.Project]);
        ws.TrackEvents();

        ws.Seed("app.enabled", FakeEditorScope.User, false);
        ws.Seed("app.label", FakeEditorScope.Project, "project-label");
        ws.Seed("app.tags", FakeEditorScope.User, (IReadOnlyList<object?>)["x", "y"]);

        IReadOnlyList<IEditorSchema> schemas =
        [
            new FakeEditorSchema("app.enabled", EditorValueType.Boolean),
            new FakeEditorSchema("app.label"),
            new FakeEditorSchema("app.tags", EditorValueType.StringArray),
        ];

        // Act: create editors
        IReadOnlyList<PropertyEditorViewModel> editors = factory.CreateForGroup(schemas, ws, FakeEditorScope.User);

        // Act: load values
        foreach (PropertyEditorViewModel ed in editors)
        {
            ed.LoadFromValue(ws.GetValue(ed.Path), FakeEditorScope.User);
        }

        BooleanPropertyEditorViewModel enabledVm = (BooleanPropertyEditorViewModel)editors[0];
        StringPropertyEditorViewModel labelVm = (StringPropertyEditorViewModel)editors[1];
        StringArrayPropertyEditorViewModel tagsVm = (StringArrayPropertyEditorViewModel)editors[2];

        // Assert: editors reflect loaded values
        Assert.False(enabledVm.Value); // set at User
        Assert.Null(labelVm.Value); // set at Project only, editing User
        Assert.Equal(2, tagsVm.Items.Count); // custom factory used
        Assert.Equal("x", tagsVm.Items[0]);

        // Act: mutate through workspace
        ws.SetValue("app.label", "user-override", FakeEditorScope.User);
        Assert.Equal(1, ws.TrackedEventCount);

        // Reload label editor
        labelVm.LoadFromValue(ws.GetValue("app.label"), FakeEditorScope.User);
        Assert.Equal("user-override", labelVm.Value);

        // Act: round-trip
        object? roundTrip = labelVm.ToValue();
        Assert.Equal("user-override", roundTrip);

        // Act: reset
        enabledVm.ResetToInheritedCommand.Execute(null);
        Assert.Null(enabledVm.Value);
        Assert.False(enabledVm.IsModified);
    }
}