// Compares the ILLink warnings of a trimmed publish against a committed baseline.
//
//   dotnet run scripts/check-trim-warnings.cs -- <publish.log> trimcheck/expected-warnings.txt
//
// Exits 0 when the SET of warnings matches the baseline exactly, 1 otherwise, printing what is new
// and what has gone.
//
// ⭐ Both directions fail, on purpose. A NEW warning is a trim hazard someone just added. A MISSING
// one means the baseline is stale or, more importantly, that ILLink did not analyse what it used to:
// the baseline's third-party warnings are reachable ONLY through ScopedEditors.AvaloniaUI, so their
// presence is what proves the publish walked that assembly at all. An empty log must never pass.
//
// Warnings are matched on "ILxxxx: member: message", with the location before it and the project
// path the SDK appends in brackets both removed, so the same warning compares equal on every
// machine and runtime identifier. They are compared as a SET, because the same text can appear
// more than once: two call sites in one method print identically.

using System.Text.RegularExpressions;

if (args.Length != 2)
{
    Console.Error.WriteLine("usage: check-trim-warnings.cs <publish.log> <expected-warnings.txt>");
    return 2;
}

Regex warning = new(@"Trim analysis warning (IL\d{4}: .+?)(?:\s+\[[^\]]+\])?\s*$", RegexOptions.Multiline);

SortedSet<string> actual = new(StringComparer.Ordinal);
foreach (Match match in warning.Matches(File.ReadAllText(args[0]).Replace("\r", "")))
{
    actual.Add(match.Groups[1].Value.Trim());
}

SortedSet<string> expected = new(
    File.ReadAllLines(args[1])
        .Select(line => line.Trim())
        .Where(line => line.Length > 0 && !line.StartsWith('#')),
    StringComparer.Ordinal);

string[] added = [.. actual.Except(expected)];
string[] gone = [.. expected.Except(actual)];

if (added.Length == 0 && gone.Length == 0)
{
    Console.WriteLine($"Trim warnings match the baseline: {actual.Count} expected, none new.");
    return 0;
}

foreach (string line in added)
{
    Console.WriteLine($"::error::NEW trim warning (a trim hazard, or an unreviewed dependency change): {line}");
}

foreach (string line in gone)
{
    Console.WriteLine($"::error::EXPECTED trim warning missing (stale baseline, or ILLink no longer analysed it): {line}");
}

Console.WriteLine();
Console.WriteLine("If the change is intended, update trimcheck/expected-warnings.txt in the same commit and say why.");
return 1;
