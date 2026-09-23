using System.Text.RegularExpressions;
using Bennewitz.Ninja.ScopedEditors.AvaloniaUI.Tests;

namespace ScopedEditors.Tests.Architecture;

/// <summary>
/// Every <c>LE.*</c> theme resource this package's markup or code asks for is declared, and every
/// one it declares is resolved by its own markup or code, or listed as kept for hosts. Carried across
/// from ClaudeForge's <c>ThemeResourceIntegrityTests</c>.
/// </summary>
/// <remarks>
/// <para>
/// ⛔⛔ <b>Why the guard exists: two tokens were referenced nine times and declared zero times, and
/// nothing in the toolchain said a word.</b> An unresolvable <c>DynamicResource</c> is not a build
/// error, because Avalonia's XAML compiler defers it to runtime. It is not a runtime error either:
/// the property keeps its default and nothing is logged. A live UI Automation and screenshot pass
/// missed it as well, because every affected element depended on a state the pass never seeded. So
/// every danger signal in one editor rendered as ordinary text inside an invisible border.
/// </para>
/// <para>
/// ⚠ <b>Scoped to <c>LE.*</c>, the namespace this package declares.</b> Avalonia's and Semi's keys
/// come from packages this test cannot enumerate. The original's third test, that every
/// <c>App*</c> token a shared library asks for is declared by every app that renders it, compares
/// the hosts with each other, so it stays with the apps.
/// </para>
/// <para>
/// ⭐ <b>C# is read as well as markup</b>, because six of the tokens are resolved from code and
/// never from markup. A mistyped key in <c>BrushHelper.Resolve</c> falls back to its hard-coded hex
/// without a sound, so both directions look at code: the typo is a key resolved but never declared,
/// and the key it was meant to be is declared but never used.
/// </para>
/// </remarks>
public sealed class ThemeResourceIntegrityTests
{
    /// <summary>
    /// <c>{DynamicResource LE.Foo}</c> or <c>{StaticResource LE.Foo}</c>. Captures the whole key, so a
    /// failure names exactly what to add to <c>EditorColors.axaml</c>.
    /// </summary>
    private static readonly Regex ReferencePattern = new(
        @"\{\s*(?:Dynamic|Static)Resource\s+(LE\.[A-Za-z0-9_]+)\s*\}",
        RegexOptions.Compiled);

    /// <summary>A resource declaration: <c>x:Key="LE.Foo"</c>.</summary>
    private static readonly Regex DefinitionPattern = new(
        @"x:Key\s*=\s*""(LE\.[A-Za-z0-9_]+)""",
        RegexOptions.Compiled);

    /// <summary>
    /// A <c>"LE.Foo"</c> string literal in C#, which is how a control or converter resolves a brush
    /// at runtime.
    /// </summary>
    /// <remarks>
    /// ⚠ Reading the literals is what keeps this honest. The original's first draft kept a hardcoded
    /// allow-list of keys "used from code", a list that silently goes stale the moment a control stops
    /// using one: the token is then dead and the list keeps vouching for it.
    /// </remarks>
    private static readonly Regex CodeReferencePattern = new(
        @"""(LE\.[A-Za-z0-9_]+)""",
        RegexOptions.Compiled);

    /// <summary>A key this package declares for hosts, and never resolves itself.</summary>
    /// <param name="Key">The resource key.</param>
    /// <param name="Reason">Why it is declared even though nothing here uses it.</param>
    private sealed record KeptForHosts(string Key, string Reason);

    /// <summary>
    /// Keys declared for hosts: the ONLY exemption from the reverse check, each with its reason.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⚠ A package cannot see its consumers, so "is this declared key used?" has no answer here for a
    /// key that exists for hosts to resolve in their own markup. Such a key is listed rather than the
    /// check dropped, and it is not deleted to quiet the check either: removing a key from a published
    /// package breaks any host that resolves it.
    /// </para>
    /// <para>
    /// ⛔ <b>The list checks itself</b>, in
    /// <see cref="Every_key_kept_for_hosts_is_still_declared_and_still_unused_here"/>. An entry whose key
    /// is no longer declared, or that this package has started to resolve, fails there. An allow-list
    /// that can outlive its reason is how a dead-token guard quietly stops mattering.
    /// </para>
    /// </remarks>
    private static readonly KeptForHosts[] KeysKeptForHosts =
    [
        new("LE.DangerBorder",
            "The border of a warning banner, paired with LE.DangerText. Its known consumer was "
            + "OpenCodeForge's keybind editor, which left OpenForge2k's tree in that repository's "
            + "plans/00003 Phase 0; no app there resolves it as of 2026-09-23."),
        new("LE.DangerText",
            "Warning and problem text, with the same known consumer and the same state as "
            + "LE.DangerBorder. The wrapper's own banner deliberately takes the severity tokens instead: "
            + "see PropertyEditorWrapper.axaml."),
    ];

    [Fact]
    public void Every_referenced_LE_key_is_declared()
    {
        TokenScan scan = Scan();

        string[] undeclared =
        [
            .. scan.ReferencedFromMarkup.Keys
                .Where(k => !scan.Declared.Contains(k))
                .Order(StringComparer.Ordinal),
        ];

        IEnumerable<string> lines = undeclared.Select(k =>
            $"  {k}  ({scan.ReferencedFromMarkup[k].Count} reference(s) in "
            + $"{string.Join(", ", scan.ReferencedFromMarkup[k])})");

        Assert.True(
            undeclared.Length == 0,
            $"{undeclared.Length} LE.* theme resource(s) are referenced but never declared:\n"
            + string.Join('\n', lines)
            + "\n\nAn unresolvable DynamicResource does not fail the build, does not throw, and logs "
            + "nothing: Avalonia leaves the property at its default, so the control just renders "
            + "unstyled. Declare each key in src/ScopedEditors.AvaloniaUI/Themes/EditorColors.axaml, or "
            + "correct the reference. Declared keys are: "
            + $"{string.Join(", ", scan.Declared.Order(StringComparer.Ordinal))}.");
    }

    /// <summary>
    /// The same question for keys resolved from C#: every <c>"LE.Foo"</c> literal names a declared key.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⚠ <b>Quieter than the markup case, not louder.</b> <c>BrushHelper.Resolve</c> falls back to its
    /// hard-coded hex, so the control still gets a colour, just never the declared default or a host's
    /// override of the key that was meant. A key that exists only in code is also missing from
    /// <c>EditorColors.axaml</c>, the token list a host reads to learn what it can override.
    /// </para>
    /// <para>
    /// Not in the original, which checked markup alone. There a typo in code surfaced only when it
    /// left a declared key unused, and a key new to code did not surface at all.
    /// </para>
    /// </remarks>
    [Fact]
    public void Every_LE_key_resolved_from_code_is_declared()
    {
        TokenScan scan = Scan();

        string[] undeclared =
        [
            .. scan.ReferencedFromCode.Keys
                .Where(k => !scan.Declared.Contains(k))
                .Order(StringComparer.Ordinal),
        ];

        IEnumerable<string> lines = undeclared.Select(k =>
            $"  {k}  (in {string.Join(", ", scan.ReferencedFromCode[k])})");

        Assert.True(
            undeclared.Length == 0,
            $"{undeclared.Length} LE.* key(s) are resolved from C# but never declared:\n"
            + string.Join('\n', lines)
            + "\n\nBrushHelper.Resolve falls back to its hard-coded hex without a sound. If the literal is "
            + "a typo, the declared default and any host override of the intended key are ignored; if it "
            + "is a new key, EditorColors.axaml, the list hosts read, does not mention it. Declare it in "
            + "src/ScopedEditors.AvaloniaUI/Themes/EditorColors.axaml, or correct the literal.");
    }

    /// <summary>
    /// The inverse direction: a declared token nothing uses is dead theme surface, or the correct
    /// spelling of a key something asks for under a typo.
    /// </summary>
    [Fact]
    public void Every_declared_LE_key_is_used_here_or_kept_for_hosts()
    {
        TokenScan scan = Scan();
        HashSet<string> kept = new(KeysKeptForHosts.Select(k => k.Key), StringComparer.Ordinal);

        string[] unused =
        [
            .. scan.Declared
                .Where(k => !scan.IsUsed(k) && !kept.Contains(k))
                .Order(StringComparer.Ordinal),
        ];

        MessageAssert.Equal(
            0,
            unused.Length,
            $"{unused.Length} LE.* theme resource(s) are declared but resolved by neither the markup nor "
            + $"the C# of {PackageSources.Project}: {string.Join(", ", unused)}.\n\n"
            + "Wire each one up or delete it, and check first whether one is the correct spelling of a key "
            + "that markup or code asks for under a typo, which "
            + $"{nameof(Every_referenced_LE_key_is_declared)} or "
            + $"{nameof(Every_LE_key_resolved_from_code_is_declared)} would then be naming. A key that "
            + "exists for hosts to resolve goes in KeysKeptForHosts, with its reason.");
    }

    [Fact]
    public void Every_key_kept_for_hosts_is_still_declared_and_still_unused_here()
    {
        TokenScan scan = Scan();
        List<string> problems = [];

        foreach (KeptForHosts entry in KeysKeptForHosts)
        {
            if (!scan.Declared.Contains(entry.Key))
            {
                problems.Add(
                    $"{entry.Key} is listed but no longer declared. Remove the entry: the list vouches "
                    + "only for keys that exist.");
            }
            else if (scan.IsUsed(entry.Key))
            {
                problems.Add(
                    $"{entry.Key} is listed, but this package now resolves it. Remove the entry, so the "
                    + "reverse check covers the key again if it ever goes unused.");
            }
        }

        Assert.True(
            problems.Count == 0,
            "KeysKeptForHosts has outlived its reasons:\n  " + string.Join("\n  ", problems));
    }

    /// <summary>
    /// What the package declares, and what its markup and its code ask for: each key with the files
    /// that ask for it, so a failure says where to look.
    /// </summary>
    private sealed record TokenScan(
        IReadOnlySet<string> Declared,
        IReadOnlyDictionary<string, SortedSet<string>> ReferencedFromMarkup,
        IReadOnlyDictionary<string, SortedSet<string>> ReferencedFromCode)
    {
        public bool IsUsed(string key) => ReferencedFromMarkup.ContainsKey(key) || ReferencedFromCode.ContainsKey(key);
    }

    private static TokenScan Scan()
    {
        HashSet<string> declared = new(StringComparer.Ordinal);
        Dictionary<string, SortedSet<string>> fromMarkup = new(StringComparer.Ordinal);
        Dictionary<string, SortedSet<string>> fromCode = new(StringComparer.Ordinal);

        foreach (PackageSources.SourceText file in PackageSources.MarkupText())
        {
            foreach (Match m in DefinitionPattern.Matches(file.Text))
            {
                declared.Add(m.Groups[1].Value);
            }

            foreach (Match m in ReferencePattern.Matches(file.Text))
            {
                AddReference(fromMarkup, m.Groups[1].Value, file.Path);
            }
        }

        foreach (PackageSources.SourceText file in PackageSources.CSharp())
        {
            foreach (Match m in CodeReferencePattern.Matches(file.Text))
            {
                AddReference(fromCode, m.Groups[1].Value, file.Path);
            }
        }

        // A scan that finds nothing proves nothing. Today the package declares 10 keys, its markup
        // asks for 2 and its code for 6. The declaration and code floors sit below those counts so
        // ordinary churn does not trip them, while a broken pattern or a collapsed scan root does.
        Assert.True(
            declared.Count >= 6,
            $"Expected at least 6 LE.* declarations under {PackageSources.Project}, found "
            + $"{declared.Count}. The scan or the declaration pattern is broken, not the package.");
        Assert.True(
            fromMarkup.Count >= 2,
            $"Expected at least 2 distinct LE.* references in the markup under {PackageSources.Project}, "
            + $"found {fromMarkup.Count}. The scan or the reference pattern is broken.");
        Assert.True(
            fromCode.Count >= 1,
            "Expected at least one LE.* brush to be resolved from C# (BoolToStatusBrushConverter and "
            + "LinkifiedTextBlock both do). Finding none means the C# scan or its pattern is broken, "
            + "which would make every code-only token look dead.");

        return new TokenScan(declared, fromMarkup, fromCode);
    }

    private static void AddReference(Dictionary<string, SortedSet<string>> references, string key, string path)
    {
        if (!references.TryGetValue(key, out SortedSet<string>? files))
        {
            files = new SortedSet<string>(StringComparer.Ordinal);
            references[key] = files;
        }

        files.Add(path);
    }
}
