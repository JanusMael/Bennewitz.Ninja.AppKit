namespace Bennewitz.Ninja.ScopedEditors.ViewModels;

/// <summary>
/// Editor for file/directory path properties. Shows a text box plus a Browse button
/// when a <see cref="EditorContext.BrowsePath"/> or <see cref="EditorContext.BrowseFile"/>
/// callback is supplied.
/// </summary>
public partial class PathPropertyEditorViewModel : PropertyEditorViewModel
{
    private readonly Func<Task<string?>>? _browseDialog;

    /// <summary>Create a path editor, optionally backed by a browse dialog.</summary>
    /// <param name="schema">Describes the property being edited.</param>
    /// <param name="editingScope">The scope edits are written to.</param>
    /// <param name="browseDialog">Opens a picker and returns the chosen path, or null if cancelled.
    /// When absent the editor is text-only.</param>
    public PathPropertyEditorViewModel(IEditorSchema schema, IEditorScope editingScope,
                                       Func<Task<string?>>? browseDialog = null)
        : base(schema, editingScope)
    {
        _browseDialog = browseDialog;
    }

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsValueSet))]
    private string? _value;

    /// <summary>True when this editor holds a value of its own (the path is non-null) rather than
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

    [RelayCommand]
    private async Task BrowseAsync()
    {
        if (_browseDialog == null)
        {
            return;
        }

        string? result = await _browseDialog();
        if (result != null)
        {
            Value = result;
        }
    }

    /// <inheritdoc/>
    protected override void OnResetToInherited()
    {
        Value = null;
    }
}