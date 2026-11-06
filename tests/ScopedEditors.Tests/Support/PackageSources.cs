using System.Text.RegularExpressions;
using Bennewitz.Ninja.XamlQuality;

namespace Bennewitz.Ninja.ScopedEditors.AvaloniaUI.Tests;

/// <summary>
/// This package's own markup and code, as the guards carried across from ClaudeForge read them.
/// </summary>
/// <remarks>
/// <para>
/// ⛔ <b>Before the split, those guards scanned this markup from inside OpenForge2k</b>, where
/// <c>src/LayeredEditors.Avalonia</c> sat beside the apps and fell inside their repository-wide
/// scans. The move carried the markup here and left the guards behind, so nothing checked it until
/// they were ported. Only the package's half came across: each app keeps its own wrapper, surfaces
/// and tokens, and guards them where they live.
/// </para>
/// <para>
/// ⭐ <b>One home for the scope, so the guards cannot drift apart.</b> They all read the same
/// project, and a second copy of that path is the one that goes stale when the project moves.
/// </para>
/// <para>
/// ⚠ <b>The text form has its XML comments removed.</b> A binding merely NAMED in prose is not a
/// binding. Measured, not hypothetical: <c>PropertyEditorWrapper.axaml</c> names
/// <c>DangerAccessibleText</c> in a comment, so with every binding to it deleted, a plain text
/// search still found the word and the danger-surface guards stayed green.
/// </para>
/// </remarks>
internal static class PackageSources
{
    /// <summary>
    /// The one project in this family that holds markup, relative to the repository root and
    /// forward-slashed like every path in a failure message.
    /// </summary>
    internal const string Project = "src/ScopedEditors.AvaloniaUI";

    private static readonly Regex XmlComment = new("<!--.*?-->", RegexOptions.Singleline | RegexOptions.Compiled);

    /// <summary>A source file: its path relative to the repository root, and its text.</summary>
    internal sealed record SourceText(string Path, string Text);

    /// <summary>
    /// Every markup file in <see cref="Project"/>, read once by XamlQuality's scan, which skips
    /// <c>bin</c> and <c>obj</c>.
    /// </summary>
    /// <remarks>
    /// ⛔ Fails rather than returns when the scan found nothing, or when a file did not parse. The
    /// XamlQuality rules pass over a file they cannot parse, so an unparseable file would otherwise
    /// read as a clean one.
    /// </remarks>
    internal static XamlScanContext Markup()
    {
        XamlScanContext markup = XamlScanContext.Load(ProjectDirectory());

        Assert.True(
            markup.Files.Count > 0,
            $"No markup under {Project}. A scan that finds nothing proves nothing: either the project "
            + "moved or the scan is looking in the wrong place.");

        string[] unparsed =
        [
            .. markup.Files
                .Where(f => f.ParseError is not null)
                .Select(f => $"{Project}/{f.RelativePath}: {f.ParseError}"),
        ];

        Assert.True(
            unparsed.Length == 0,
            "Markup that does not parse is passed over by every structural rule, so it would read as "
            + "clean:\n  " + string.Join("\n  ", unparsed));

        return markup;
    }

    /// <summary>The markup as text, with its XML comments removed, in path order.</summary>
    internal static IReadOnlyList<SourceText> MarkupText() =>
    [
        .. Markup().Files.Select(f => new SourceText(
            $"{Project}/{f.RelativePath}",
            XmlComment.Replace(f.Text, string.Empty))),
    ];

    /// <summary>Every C# file in <see cref="Project"/>, verbatim, in path order.</summary>
    internal static IReadOnlyList<SourceText> CSharp()
    {
        string root = ProjectDirectory();
        List<SourceText> files = [];

        foreach (string path in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(root, path).Replace('\\', '/');

            // Build output holds generated C#, which would count a key as used by code nobody wrote.
            // Segment-wise, so a real folder that merely contains the letters is not skipped.
            if (relative.Split('/').Any(s => s.Equals("bin", StringComparison.OrdinalIgnoreCase)
                                             || s.Equals("obj", StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            files.Add(new SourceText($"{Project}/{relative}", File.ReadAllText(path)));
        }

        Assert.True(
            files.Count > 0,
            $"No C# under {Project}. Either the project moved or the scan is looking in the wrong place.");

        return [.. files.OrderBy(f => f.Path, StringComparer.Ordinal)];
    }

    /// <summary>
    /// The repository root: the nearest directory above the test output that holds a <c>.slnx</c>.
    /// </summary>
    internal static string RepoRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null && !Directory.EnumerateFiles(directory.FullName, "*.slnx").Any())
        {
            directory = directory.Parent;
        }

        MessageAssert.NotNull(directory, $"No *.slnx above {AppContext.BaseDirectory}, so there is no source to scan.");
        return directory.FullName;
    }

    private static string ProjectDirectory() =>
        Path.Combine(Project.Split('/').Prepend(RepoRoot()).ToArray());
}
