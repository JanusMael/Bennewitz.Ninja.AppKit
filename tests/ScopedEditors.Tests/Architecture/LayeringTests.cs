using System.Reflection;
using System.Xml.Linq;

namespace ScopedEditors.Tests.Architecture;

/// <summary>
/// Enforces what each package in this family is allowed to reach. Carried across from ClaudeForge's
/// <c>AssemblyLayeringTests</c> in step 5 of plan 00002, because moving the code out of that
/// repository left its guards behind.
/// </summary>
/// <remarks>
/// <para>
/// ⛔ <b>Two checks, because either alone has a blind spot.</b> The csproj check reads the project
/// files and is the leading indicator: a bad reference is an architectural decision the moment it
/// is written. The reflection check reads compiled reference tables and catches what arrives
/// without a direct <c>ProjectReference</c>.
/// </para>
/// <para>
/// ⛔ <b>The csproj check is not redundant.</b> The compiler <b>omits unused references from the
/// assembly reference table entirely</b>, so a declared-but-not-yet-used bad reference is invisible
/// to reflection — which is the state a violation is in right before someone depends on it.
/// Demonstrated rather than asserted: mutating this family's sibling repository showed the project
/// check failing while the reflection check stayed green.
/// </para>
/// <para>
/// ⭐ <b>The invariant that matters most here is that <c>ScopedEditors.ViewModels</c> sees no UI
/// framework.</b> It is what keeps editors safe to build off the UI thread — a control reference is
/// a build error rather than a race nobody reproduces. The compiler enforces it today only because
/// nothing has added the reference; this makes the intent explicit so adding one fails loudly.
/// </para>
/// </remarks>
public sealed class LayeringTests
{
    /// <summary>What one project in this family is allowed to reach.</summary>
    // Internal, not private: AssemblyQualityTests reads this same table for AQ1003, so the tiers
    // have ONE home. A second copy would be the list that silently rots.
    internal sealed record Tier(string Project, string[] MayReferenceProjects, string[] ForbiddenPackages);

    internal static readonly Tier[] Tiers =
    [
        // Zero outgoing edges, deliberately. Not "few" — none.
        new("ScopedEditors.Abstractions", [], ["Avalonia", "Semi", "Serilog", "CommunityToolkit"]),

        // ⛔ No UI framework, at all. This is the one that keeps editors off-thread-safe.
        new("ScopedEditors.ViewModels", ["ScopedEditors.Abstractions"], ["Avalonia", "Semi", "Serilog"]),

        // The only tier allowed to see Avalonia, and the only id binding a consumer to Semi.
        new("ScopedEditors.AvaloniaUI",
            ["ScopedEditors.Abstractions", "ScopedEditors.ViewModels"],
            ["Serilog"]),
    ];

    /// <summary>
    /// Name fragments belonging to another repository. ⚠ The two families share no edge — measured,
    /// not assumed — so either could be versioned or abandoned without touching the other.
    /// </summary>
    internal static readonly string[] ForeignFamilies = ["AppServices", "LayeredEditors", "ClaudeForge", "AgentForge"];

    [Fact]
    public void Every_tier_named_here_actually_exists()
    {
        // ⛔ Without this, renaming a project turns every assertion below into a no-op pass.
        foreach (Tier tier in Tiers)
        {
            Assert.True(
                File.Exists(ProjectFile(tier.Project)),
                $"{tier.Project} is named by these tests but does not exist. Either it was renamed "
                + "(update this file) or it is gone, in which case its layering is unguarded.");
        }
    }

    [Fact]
    public void No_project_reaches_outside_its_tier()
    {
        List<string> violations = [];

        foreach (Tier tier in Tiers)
        {
            foreach (string reference in ProjectReferences(tier.Project))
            {
                // Every path segment, not just the file name: a project file need not be named
                // after its directory, so a renamed file inside the right folder is a real
                // violation a file-name-only check waves through.
                string[] segments = reference.Replace('\\', '/')
                    .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                string referenced = Path.GetFileNameWithoutExtension(segments[^1]);

                if (!tier.MayReferenceProjects.Contains(referenced, StringComparer.Ordinal))
                {
                    violations.Add($"{tier.Project} -> {referenced}");
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            "A project declares a reference its tier does not allow:\n  "
            + string.Join("\n  ", violations)
            + "\n\nScopedEditors.Abstractions in particular must reach nothing at all.");
    }

    [Fact]
    public void No_project_declares_a_package_its_tier_forbids()
    {
        List<string> violations = [];

        foreach (Tier tier in Tiers)
        {
            foreach (string package in PackageReferences(tier.Project))
            {
                foreach (string forbidden in tier.ForbiddenPackages)
                {
                    if (package.StartsWith(forbidden, StringComparison.Ordinal))
                    {
                        violations.Add($"{tier.Project} -> {package}");
                    }
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            "A project declares a package reference its tier forbids:\n  "
            + string.Join("\n  ", violations)
            + "\n\nViewModels touching Avalonia is the one that matters: it is what lets an editor "
            + "be built off the UI thread, and a single control reference ends that for everyone.");
    }

    [Fact]
    public void Nothing_here_reaches_another_family()
    {
        List<string> violations = [];

        foreach (Tier tier in Tiers)
        {
            IEnumerable<string> all = ProjectReferences(tier.Project).Concat(PackageReferences(tier.Project));

            foreach (string reference in all)
            {
                foreach (string foreign in ForeignFamilies)
                {
                    if (reference.Contains(foreign, StringComparison.Ordinal))
                    {
                        violations.Add($"{tier.Project} -> {reference}");
                    }
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            "A project in this family references another one:\n  " + string.Join("\n  ", violations)
            + "\n\nThe two families share no edge, which is what lets either be versioned or "
            + "abandoned without touching the other.");
    }

    [Fact]
    public void No_compiled_assembly_reaches_another_family()
    {
        string output = Path.GetDirectoryName(typeof(LayeringTests).Assembly.Location)!;
        string[] assemblies = [.. Directory.GetFiles(output, "ScopedEditors*.dll")];

        Assert.True(
            assemblies.Length > 0,
            $"No ScopedEditors assembly in {output} — this test cannot see what it is guarding.");

        List<string> violations = [];

        foreach (string path in assemblies)
        {
            string name;
            AssemblyName[] referenced;
            try
            {
                Assembly assembly = Assembly.LoadFrom(path);
                name = assembly.GetName().Name ?? Path.GetFileNameWithoutExtension(path);
                referenced = assembly.GetReferencedAssemblies();
            }
            catch (Exception ex) when (ex is BadImageFormatException or FileLoadException)
            {
                continue;
            }

            foreach (AssemblyName reference in referenced)
            {
                string referencedName = reference.Name ?? string.Empty;

                if (ForeignFamilies.Any(f => referencedName.Contains(f, StringComparison.Ordinal)))
                {
                    violations.Add($"{name} -> {referencedName}");
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            "A compiled assembly references another family:\n  " + string.Join("\n  ", violations));
    }

    [Fact]
    public void Abstractions_compiles_against_nothing_but_the_framework()
    {
        string[] nonFramework = NonFrameworkReferencesOf("ScopedEditors.Abstractions.dll");

        Assert.True(
            nonFramework.Length == 0,
            "ScopedEditors.Abstractions has grown a reference:\n  " + string.Join("\n  ", nonFramework)
            + "\n\nIt is the package a test or a non-UI host consumes. Reaching anything is the "
            + "change that quietly undoes the split.");
    }

    [Fact]
    public void ViewModels_compiles_without_any_UI_framework()
    {
        // ⭐ Asserted against the COMPILED assembly, not the project file, so a UI reference
        // arriving through a props file or transitively is caught too. This is the property that
        // makes an editor safe to build off the UI thread.
        string[] ui =
        [
            .. NonFrameworkReferencesOf("ScopedEditors.ViewModels.dll")
                .Where(n => n.Contains("Avalonia", StringComparison.Ordinal)
                            || n.Contains("Semi", StringComparison.Ordinal)),
        ];

        Assert.True(
            ui.Length == 0,
            "ScopedEditors.ViewModels has grown a UI-framework reference:\n  " + string.Join("\n  ", ui)
            + "\n\nA control reference here is supposed to be a BUILD error, which is what keeps "
            + "editors constructible off the UI thread. Move the code into ScopedEditors.AvaloniaUI.");
    }

    // ── reading the outputs and the project files ────────────────────────────

    private static string[] NonFrameworkReferencesOf(string assemblyFileName)
    {
        string output = Path.GetDirectoryName(typeof(LayeringTests).Assembly.Location)!;
        string path = Path.Combine(output, assemblyFileName);

        Assert.True(File.Exists(path), $"{assemblyFileName} is not in {output}.");

        return
        [
            .. Assembly.LoadFrom(path)
                .GetReferencedAssemblies()
                .Select(a => a.Name ?? string.Empty)
                .Where(n => !n.StartsWith("System", StringComparison.Ordinal)
                            && !n.Equals("netstandard", StringComparison.Ordinal)
                            && !n.Equals("mscorlib", StringComparison.Ordinal)),
        ];
    }

    private static string ProjectFile(string project) =>
        Path.Combine(RepoRoot(), "src", project, project + ".csproj");

    private static IReadOnlyList<string> ProjectReferences(string project) =>
        Attributes(project, "ProjectReference");

    private static IReadOnlyList<string> PackageReferences(string project) =>
        Attributes(project, "PackageReference");

    private static IReadOnlyList<string> Attributes(string project, string element) =>
    [
        .. XDocument.Load(ProjectFile(project))
            .Descendants()
            .Where(e => e.Name.LocalName == element)
            .Select(e => e.Attribute("Include")?.Value)
            .Where(v => !string.IsNullOrEmpty(v))
            .Select(v => v!),
    ];

    private static string RepoRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null && !Directory.EnumerateFiles(directory.FullName, "*.slnx").Any())
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory.FullName;
    }
}
