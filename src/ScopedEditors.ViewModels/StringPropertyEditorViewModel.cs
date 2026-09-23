namespace Bennewitz.Ninja.ScopedEditors.ViewModels;

/// <summary>Editor for general string properties.</summary>
public partial class StringPropertyEditorViewModel : PropertyEditorViewModel
{
    /// <summary>Create an editor for <paramref name="schema"/> that writes to
    /// <paramref name="editingScope"/>.</summary>
    /// <param name="schema">Describes the property being edited.</param>
    /// <param name="editingScope">The scope edits are written to.</param>
    public StringPropertyEditorViewModel(IEditorSchema schema, IEditorScope editingScope)
        : base(schema, editingScope)
    {
    }

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsValueSet))]
    private string? _value;

    /// <summary>True when this editor holds a value of its own (the string is non-null) rather than
    /// inheriting one from a broader scope.</summary>
    public bool IsValueSet => Value != null;

    partial void OnValueChanged(string? value)
    {
        TrackValueSet(value != null);
    }

    /// <inheritdoc/>
    public override object? ToValue()
    {
        return Value;
    }

    /// <inheritdoc/>
    public override void LoadFromValue(IEditorValue value, IEditorScope editingScope)
    {
        EditingScope = editingScope;
        EffectiveScope = value.EffectiveScope;
        IsOverridden = value.IsOverridden;

        object? scopeValue = value.GetValueAt(editingScope);
        Value = scopeValue as string;
        IsModified = Value != null;
        UpdateOtherScopesWithData(value, editingScope);
        UpdateInheritedDisplay(value, editingScope);
    }

    /// <inheritdoc/>
    protected override void OnResetToInherited()
    {
        Value = null;
    }
}