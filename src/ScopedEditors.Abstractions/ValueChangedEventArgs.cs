namespace Bennewitz.Ninja.ScopedEditors.Abstractions;

/// <summary>
/// Raised by <see cref="IEditorWorkspace.ValueChanged"/> after a successful
/// <c>SetValue</c> or <c>RemoveValue</c>. Editors subscribe and filter by
/// <see cref="Path"/> prefix to decide whether to refresh.
/// </summary>
public sealed class ValueChangedEventArgs : EventArgs
{
    /// <summary>Record that one value changed in one scope.</summary>
    /// <param name="path">The path that changed.</param>
    /// <param name="scope">The scope the change was written to.</param>
    public ValueChangedEventArgs(string path, IEditorScope scope)
    {
        Path = path;
        Scope = scope;
    }

    /// <summary>The path that changed — identical to <see cref="IEditorValue.Path"/>.</summary>
    public string Path { get; }

    /// <summary>The scope the change was applied to.</summary>
    public IEditorScope Scope { get; }
}