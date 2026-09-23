using Bennewitz.Ninja.ScopedEditors.Abstractions;
using Bennewitz.Ninja.ScopedEditors.Avalonia.Converters;

namespace ScopedEditors.Tests;

/// <summary>
/// Exercises types that moved out of OpenForge2k and were renamed on the way, so the split and the
/// rename are proven by running the code rather than by the fact that it compiled.
/// </summary>
/// <remarks>
/// ⚠ Smoke tests, not the layering guards. Those are step 5 of plan 00002 and must scan csproj XML
/// as well as reflection: the compiler omits an unused reference from the assembly reference table,
/// so a declared-but-unused bad reference is invisible to reflection alone.
/// </remarks>
public sealed class MovedSourceSmokeTests
{
    [Fact]
    public void Value_changed_args_carry_the_path_and_the_scope_that_changed()
    {
        FakeScope scope = new("user");

        ValueChangedEventArgs args = new("editor.fontSize", scope);

        Assert.Equal("editor.fontSize", args.Path);
        Assert.Same(scope, args.Scope);
    }

    [Theory]
    [InlineData(AppSeverity.Neutral)]
    [InlineData(AppSeverity.Info)]
    [InlineData(AppSeverity.Caution)]
    [InlineData(AppSeverity.Critical)]
    public void Every_severity_has_a_glyph(AppSeverity severity)
    {
        string glyph = AppSeverityToGlyphConverter.GlyphFor(severity);

        Assert.False(string.IsNullOrWhiteSpace(glyph));
    }

    [Fact]
    public void Severity_glyphs_do_not_collide()
    {
        // The size scales in AppSeverityToFontSizeConverter are only meaningful if each severity
        // draws a different mark, so a collision is a real defect rather than a cosmetic one.
        AppSeverity[] all = Enum.GetValues<AppSeverity>();

        string[] glyphs = [.. all.Select(AppSeverityToGlyphConverter.GlyphFor)];

        Assert.Equal(glyphs.Length, glyphs.Distinct().Count());
    }

    private sealed class FakeScope(string id) : IEditorScope
    {
        public int Priority => 0;

        public string Id { get; } = id;

        public string DisplayName => Id;

        public bool IsReadOnly => false;
    }
}
