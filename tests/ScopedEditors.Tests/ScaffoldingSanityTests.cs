using System.Reflection;

namespace Bennewitz.Ninja.ScopedEditors.AvaloniaUI.Tests;

/// <summary>
/// Placeholder until the real test suite lands in later phases. Exists so the
/// test project produces a runnable assembly and `dotnet test` has something to
/// execute while the refactor is mid-flight.
/// </summary>
public sealed class ScaffoldingSanityTests
{
    [Fact]
    public void AbstractionsAreReferenced()
    {
        // Sanity: the library's interfaces are visible from the test project.
        Assert.True(typeof(IEditorSchema).IsInterface);
        Assert.True(typeof(IEditorValue).IsInterface);
        Assert.True(typeof(IEditorScope).IsInterface);
        Assert.True(typeof(IEditorWorkspace).IsInterface);
    }

    [Fact]
    public void AvaloniaPackageIsReferenced()
    {
        Assembly avaloniaAsm = typeof(AssemblyMarker).Assembly;
        Assert.NotNull(avaloniaAsm);
    }
}