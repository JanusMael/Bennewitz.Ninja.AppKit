namespace Bennewitz.Ninja.ScopedEditors.ViewModels;

/// <summary>
/// Editor for integer and floating-point number properties, with optional min/max bounds.
/// </summary>
public partial class NumberPropertyEditorViewModel : PropertyEditorViewModel
{
    /// <summary>Create an editor for <paramref name="schema"/> that writes to
    /// <paramref name="editingScope"/>.</summary>
    /// <param name="schema">Describes the property being edited.</param>
    /// <param name="editingScope">The scope edits are written to.</param>
    public NumberPropertyEditorViewModel(IEditorSchema schema, IEditorScope editingScope)
        : base(schema, editingScope)
    {
        Minimum = schema.Minimum;
        Maximum = schema.Maximum;
        IsInteger = schema.ValueType == EditorValueType.Integer;
    }

    /// <summary>Inclusive lower bound from the schema, or <see langword="null"/> when it sets none.</summary>
    public double? Minimum { get; }
    /// <summary>Inclusive upper bound from the schema, or <see langword="null"/> when it sets none.</summary>
    public double? Maximum { get; }

    /// <summary>True when the schema describes an integer (controls spinner increment and round-trip).</summary>
    public bool IsInteger { get; }

    /// <summary><c>null</c> = not set at this scope (inherits).</summary>
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsValueSet))]
    private double? _value;

    /// <summary>True when this editor holds a value of its own (the nullable has a value) rather than
    /// inheriting one from a broader scope.</summary>
    public bool IsValueSet => Value.HasValue;

    partial void OnValueChanged(double? value)
    {
        TrackValueSet(value.HasValue);
    }

    /// <inheritdoc/>
    public override object? ToValue()
    {
        if (!Value.HasValue)
        {
            return null;
        }

        return IsInteger ? (object)(long)Value.Value : Value.Value;
    }

    /// <inheritdoc/>
    public override void LoadFromValue(IEditorValue value, IEditorScope editingScope)
    {
        EditingScope = editingScope;
        EffectiveScope = value.EffectiveScope;
        IsOverridden = value.IsOverridden;

        object? scopeValue = value.GetValueAt(editingScope);
        if (scopeValue is long l)
        {
            Value = l;
            IsModified = true;
        }
        else if (scopeValue is double d)
        {
            Value = d;
            IsModified = true;
        }
        else if (scopeValue is int i)
        {
            Value = i;
            IsModified = true;
        }
        else
        {
            Value = null;
            IsModified = false;
        }

        UpdateOtherScopesWithData(value, editingScope);
        UpdateInheritedDisplay(value, editingScope);
    }

    /// <inheritdoc/>
    protected override void OnResetToInherited()
    {
        Value = null;
    }
}