using Bennewitz.Ninja.ScopedEditors.AvaloniaUI.Behaviors;

namespace Bennewitz.Ninja.ScopedEditors.AvaloniaUI.Tests.Behaviors;

/// <summary>
/// Unit tests for the pure-helper surface of the
/// <see cref="ScopedEditors.AvaloniaUI.Behaviors.FileDrop"/> attached
/// behaviour.  The visual-tree-coupled parts (Avalonia property-change
/// hookup, <c>DragOverEvent</c> / <c>DropEvent</c> wiring) require a
/// headless Avalonia harness and are covered by the integration tests
/// in <c>BackupRestoreViewModelTests.RestoreFromDroppedArchive_*</c>
/// which exercise the end-to-end VM-command path.
///
/// This class pins the extension-filter logic — the user-visible
/// "accept .zip but not .json" contract — so the AXAML
/// <c>AllowedExtensions="zip"</c> string contract stays stable.
/// </summary>
public sealed class FileDropTests
{
    // ── ParseExtensions ──────────────────────────────────────────────────────

    [Fact]
    public void ParseExtensions_Null_ReturnsNullForAnyAccept()
    {
        MessageAssert.Null(FileDrop.ParseExtensions(null), "Null input must return null (the 'accept any file' sentinel) so the " +
            "behaviour skips per-extension filtering for callers that omit the property.");
    }

    [Fact]
    public void ParseExtensions_EmptyOrWhitespace_ReturnsNullForAnyAccept()
    {
        Assert.Null(FileDrop.ParseExtensions(""));
        Assert.Null(FileDrop.ParseExtensions("   "));
    }

    [Fact]
    public void ParseExtensions_SingleEntry_StripsLeadingDotAndLowercases()
    {
        Assert.Equal(new[] { "zip" }, FileDrop.ParseExtensions("zip"));
        Assert.Equal(new[] { "zip" }, FileDrop.ParseExtensions(".zip"));
        Assert.Equal(new[] { "zip" }, FileDrop.ParseExtensions("ZIP"));
        Assert.Equal(new[] { "zip" }, FileDrop.ParseExtensions(".ZIP"));
    }

    [Fact]
    public void ParseExtensions_MultipleEntries_TrimsAndNormalises()
    {
        Assert.Equal(new[] { "zip", "json" }, FileDrop.ParseExtensions("zip,json"));
        Assert.Equal(new[] { "zip", "json" }, FileDrop.ParseExtensions(" zip , json "));
        Assert.Equal(new[] { "zip", "json" }, FileDrop.ParseExtensions(".ZIP, .Json"));
    }

    [Fact]
    public void ParseExtensions_EmptyEntries_AreDropped()
    {
        // Tolerate trailing commas / repeated separators rather than producing
        // empty-string entries that would fail to match anything.
        Assert.Equal(new[] { "zip" }, FileDrop.ParseExtensions("zip,"));
        Assert.Equal(new[] { "zip" }, FileDrop.ParseExtensions(",zip"));
        Assert.Equal(new[] { "zip", "json" }, FileDrop.ParseExtensions("zip,,json"));
    }

    // ── HasAcceptedExtension ─────────────────────────────────────────────────

    [Fact]
    public void HasAcceptedExtension_NullAllowedList_AcceptsAnyFile()
    {
        // Null allowed-list is the "accept any file" sentinel returned by
        // ParseExtensions when AllowedExtensions is unset.  Must accept
        // every reasonable filename, including files with no extension at
        // all.
        Assert.True(FileDrop.HasAcceptedExtension("foo.zip", null));
        Assert.True(FileDrop.HasAcceptedExtension("anything.txt", null));
        Assert.True(FileDrop.HasAcceptedExtension("README", null));
    }

    [Fact]
    public void HasAcceptedExtension_EmptyAllowedList_AcceptsAnyFile()
    {
        // Empty array (vs null) treated identically — same "accept any"
        // semantic since the user expressed no filter.
        Assert.True(FileDrop.HasAcceptedExtension("foo.zip", []));
    }

    [Fact]
    public void HasAcceptedExtension_MatchingExtension_Accepted()
    {
        Assert.True(FileDrop.HasAcceptedExtension("backup-2026.zip", ["zip"]));
        Assert.True(FileDrop.HasAcceptedExtension("profile.json", ["zip", "json"]));
    }

    [Fact]
    public void HasAcceptedExtension_CaseInsensitive()
    {
        // Real-world drag from Windows Explorer often surfaces filenames
        // case-preserved from the user's typing; the filter must NOT
        // reject "FILE.ZIP" when the AXAML says AllowedExtensions="zip".
        Assert.True(FileDrop.HasAcceptedExtension("BACKUP.ZIP", ["zip"]));
        Assert.True(FileDrop.HasAcceptedExtension("Backup.Zip", ["zip"]));
        Assert.True(FileDrop.HasAcceptedExtension("backup.zip", ["zip"]));
    }

    [Fact]
    public void HasAcceptedExtension_NonMatchingExtension_Rejected()
    {
        Assert.False(FileDrop.HasAcceptedExtension("photo.png", ["zip"]));
        Assert.False(FileDrop.HasAcceptedExtension("doc.txt", ["zip", "json"]));
    }

    [Fact]
    public void HasAcceptedExtension_NoExtension_Rejected()
    {
        // A file without a dot must NOT match a filtered list — the user
        // explicitly asked for .zip and the dropped item has no extension
        // at all, so the answer is no.
        Assert.False(FileDrop.HasAcceptedExtension("README", ["zip"]));
        Assert.False(FileDrop.HasAcceptedExtension("Makefile", ["zip", "json"]));
    }

    [Fact]
    public void HasAcceptedExtension_EmptyFileName_Rejected()
    {
        Assert.False(FileDrop.HasAcceptedExtension("", ["zip"]));
        Assert.False(FileDrop.HasAcceptedExtension("", null));
    }

    [Fact]
    public void HasAcceptedExtension_MultiDotName_UsesLastSegment()
    {
        // backup.2026-05.zip → extension is "zip" (last segment after the
        // final dot), not "2026-05.zip".  Important for date-stamped
        // filenames which the Backup feature itself produces.
        Assert.True(FileDrop.HasAcceptedExtension("backup.2026-05.zip", ["zip"]));
        Assert.True(FileDrop.HasAcceptedExtension("archive.tar.gz", ["gz"]));
        Assert.False(FileDrop.HasAcceptedExtension("archive.tar.gz", ["tar"]));
    }
}