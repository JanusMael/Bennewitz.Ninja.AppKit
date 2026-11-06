namespace Bennewitz.Ninja.ScopedEditors.ViewModels;

/// <summary>
/// Editor for boolean properties. Tri-state: <c>null</c> = unset (inherits),
/// <c>true</c>, <c>false</c>.
/// </summary>
public partial class BooleanPropertyEditorViewModel : PropertyEditorViewModel
{
    /// <summary>Create an editor for <paramref name="schema"/> that writes to
    /// <paramref name="editingScope"/>.</summary>
    /// <param name="schema">Describes the property being edited.</param>
    /// <param name="editingScope">The scope edits are written to.</param>
    public BooleanPropertyEditorViewModel(IEditorSchema schema, IEditorScope editingScope)
        : base(schema, editingScope)
    {
    }

    /// <summary><c>null</c> means "not set at this scope" — inherits from a lower-priority scope.</summary>
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsValueSet))]
    private bool? _value;

    /// <summary>True when this editor holds a value of its own (the nullable has a value) rather than
    /// inheriting one from a broader scope.</summary>
    public bool IsValueSet => Value.HasValue;

    partial void OnValueChanged(bool? value)
    {
        TrackValueSet(value.HasValue);
    }

    /// <inheritdoc/>
    public override object? ToValue()
    {
        return Value.HasValue ? Value.Value : null;
    }

    /// <inheritdoc/>
    public override void LoadFromValue(IEditorValue value, IEditorScope editingScope)
    {
        EditingScope = editingScope;
        EffectiveScope = value.EffectiveScope;
        IsOverridden = value.IsOverridden;

        object? scopeValue = value.GetValueAt(editingScope);
        if (scopeValue is bool b)
        {
            Value = b;
            IsModified = true;
        }
        else
        {
            Value = null;
            IsModified = false;
        }

        // populate the inheritance affordances so the wrapper
        // renders "Defined in scopes:" + "Currently effective from {scope}"
        // the same way compound editors already do.
        UpdateOtherScopesWithData(value, editingScope);
        UpdateInheritedDisplay(value, editingScope);
    }

    /// <inheritdoc/>
    protected override void OnResetToInherited()
    {
        Value = null;
    }
}