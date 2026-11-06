using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace Bennewitz.Ninja.DiffView.Tests;

/// <summary>
/// The family's ids in <c>.github/repository.json</c> are exactly what DiffView packs, and each is in
/// <c>packages.push</c>.
/// </summary>
/// <remarks>
/// ⛔ <b>A published id cannot be withdrawn, only unlisted</b>, so a project made packable and left out of
/// the family, or an id the family names and nothing packs, is found here and not on nuget.org. The
/// release takes its ids from the family through <c>scripts/release-target.cs</c>, which Templates
/// tests, and <c>PackagingTests.The_release_workflow_names_the_packages_it_pushes</c> holds that it does.
/// Compared as sets, because the order of either list says nothing.
/// </remarks>
public sealed class PackagesPushTests
{
    [Fact]
    public void The_file_names_exactly_what_the_solution_packs_and_the_release_publishes()
    {
        string[] family = FamilyIds();
        string[] packed = [.. PackagingTests.PackableProjects().Select(PackageIdOf).Order(StringComparer.Ordinal)];

        Assert.True(
            packed.Length > 0,
            "The reading found no packable project under src, so there is nothing to hold the family to.");

        Assert.True(
            packed.SequenceEqual(family),
            $"DiffView packs {string.Join(", ", packed)}, and its family names {string.Join(", ", family)}. "
            + "Classify every packable project: a published id cannot be withdrawn.");

        string[] pushed = PushedIds();
        string[] undeclared = [.. family.Except(pushed, StringComparer.Ordinal)];
        Assert.True(
            undeclared.Length == 0,
            $"The family names {string.Join(", ", undeclared)}, which packages.push does not, so the release would refuse it.");
    }

    /// <summary>The ids <c>.github/repository.json</c>'s <c>diffview</c> family names, in ordinal order.</summary>
    private static string[] FamilyIds()
    {
        JsonNode? root = JsonNode.Parse(File.ReadAllText(Path.Combine(RepoPaths.Root, ".github", "repository.json")));
        JsonArray ids = root?["families"]?["diffview"]?.AsArray()
            ?? throw new InvalidOperationException(".github/repository.json declares no diffview family.");

        return [.. ids.Select(id => id!.GetValue<string>()).Order(StringComparer.Ordinal)];
    }

    /// <summary>The ids <c>packages.push</c> names, read as <c>repo-conventions</c> reads them.</summary>
    private static string[] PushedIds() =>
    [
        .. File.ReadLines(Path.Combine(RepoPaths.Root, "packages.push"))
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#')),
    ];

    /// <summary>A project's <c>PackageId</c>, or the assembly name it falls back to where it declares none.</summary>
    private static string PackageIdOf(string project) =>
        XDocument.Load(project).Descendants("PackageId").FirstOrDefault()?.Value.Trim()
        ?? Path.GetFileNameWithoutExtension(project);
}
