#!/usr/bin/env dotnet
// Resolves what a release publishes, so the release workflow's irreversible steps take it from one
// tested place instead of from bash: the tag, or a dispatch's inputs, into the version, the tag, the
// family and the package ids (plans/00009 in Bennewitz.Ninja.Templates, decisions 9 to 13).
//
// ⭐ TWO KINDS OF REPOSITORY, told apart by "families" in .github/repository.json.
//   Without it, everything packages.push names releases together, from a `v<version>` tag.
//   With it, each family releases alone, from a `<family>/v<version>` tag, and publishes only the ids
//   its entry names; a dispatch names the family beside the version.
// Each kind refuses the other's tag, and a repository with families refuses a dispatch without one,
// loudly and before anything is built: a tag must never publish something it does not name.
//
// ⛔ THE VERSION IS CHECKED HERE: YYYY.Q.MMDD, three parts, Q the quarter of the date. A published
// version is permanent, so a malformed one is refused while that is still free. Templates'
// 2026.3.1001 went out from a tag nothing checked; 1 October is in the fourth quarter.
//
// ⭐ A FAMILY'S RELEASE NOTES are its own (plans/00011 decision 6): the commits since its previous tag
// that touched its projects or its README, and those descending from that tag that touched a build
// input every package shares. GitHub's generated notes list every pull request in the repository.
//
// Usage:
//   dotnet run scripts/release-target.cs -- resolve --event push --ref <tag> [--github-env <file>]
//   dotnet run scripts/release-target.cs -- resolve --event workflow_dispatch --version <v> [--family <f>] [--github-env <file>]
//   dotnet run scripts/release-target.cs -- projects --family <f> [--out <file>]
//   dotnet run scripts/release-target.cs -- notes --family <f> [--previous <tag>] --out <file>
//
//   resolve      prints VERSION, TAG, FAMILY, IDS (space-separated), PREVIOUS_TAG and LATEST as
//                KEY=value lines, and appends them to the --github-env file. FAMILY, PREVIOUS_TAG and
//                LATEST are empty without families. PREVIOUS_TAG is the family's newest tag below this
//                version, the bare v<version> tags counting as the family's that "bareTags" names.
//                LATEST is false when a tag of any family, or a bare one, has a higher version.
//                Read with `git tag`, so the checkout needs the tags.
//   projects     prints, and writes to the --out file, the solution's project that packs each of
//                the family's ids, as MSBuild evaluates it. Run it after the build, once restored.
//   notes        writes the family's release notes, from --previous to HEAD, to the --out file. With
//                no previous tag it says the release is the family's first. Run it once restored.
//   --root DIR   read the repository from DIR instead of the current directory
//
// Exit codes: 0 resolved, 1 refused, 2 usage.

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

string? verb = args.Length > 0 ? args[0] : null;
if (verb is not ("resolve" or "projects" or "notes"))
{
    return Usage("the first argument must be 'resolve', 'projects' or 'notes'.");
}

string? eventName = null, refName = null, version = null, family = null, githubEnv = null, outFile = null, root = null, previousTag = null;
for (int i = 1; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--event" when i + 1 < args.Length: eventName = args[++i]; break;
        case "--ref" when i + 1 < args.Length: refName = args[++i]; break;
        case "--version" when i + 1 < args.Length: version = args[++i]; break;
        case "--family" when i + 1 < args.Length: family = args[++i]; break;
        case "--github-env" when i + 1 < args.Length: githubEnv = args[++i]; break;
        case "--out" when i + 1 < args.Length: outFile = args[++i]; break;
        case "--root" when i + 1 < args.Length: root = args[++i]; break;
        case "--previous" when i + 1 < args.Length: previousTag = args[++i]; break;
        default: return Usage($"unknown or incomplete argument '{args[i]}'.");
    }
}

string repository = Path.GetFullPath(root ?? Directory.GetCurrentDirectory());

try
{
    return verb switch
    {
        "resolve" => Resolve(),
        "projects" => Projects(),
        _ => Notes(),
    };
}
catch (Refused refused)
{
    Console.Error.WriteLine("::error::" + refused.Message + " Nothing was published.");
    return 1;
}

int Resolve()
{
    if (eventName is not ("push" or "workflow_dispatch"))
    {
        return Usage("resolve needs --event push or --event workflow_dispatch.");
    }
    if (eventName == "push" && string.IsNullOrWhiteSpace(refName))
    {
        return Usage("a push needs --ref, the tag.");
    }

    IReadOnlyDictionary<string, string[]>? families = Families.Read(repository);
    (string releaseFamily, string releaseVersion) = eventName == "push"
        ? Release.FromTag(refName!.Trim(), families)
        : Release.FromDispatch(version ?? "", family ?? "", families);
    Release.CheckVersion(releaseVersion);

    string[] pushed = Lists.Read(Path.Combine(repository, "packages.push"));
    string[] ids = releaseFamily.Length == 0 ? pushed : families![releaseFamily];
    if (ids.Length == 0)
    {
        throw new Refused("packages.push names no id, so a release would publish nothing.");
    }
    string[] undeclared = [.. ids.Where(id => !pushed.Contains(id, StringComparer.OrdinalIgnoreCase))];
    if (undeclared.Length > 0)
    {
        throw new Refused($"The {releaseFamily} family names {string.Join(", ", undeclared)}, which packages.push does not. The families' ids must be exactly packages.push; `repo-conventions check` says the same.");
    }

    string tag = releaseFamily.Length == 0 ? "v" + releaseVersion : $"{releaseFamily}/v{releaseVersion}";
    string previous = "", latest = "";
    if (releaseFamily.Length > 0)
    {
        string[] tags = Tags.List(repository);
        bool ownsBareTags = Families.BareTags(repository, families!) == releaseFamily;
        previous = Tags.Previous(tags, releaseFamily, releaseVersion, ownsBareTags);
        latest = Tags.Latest(tags, releaseVersion) ? "true" : "false";
    }

    string[] lines =
    [
        "VERSION=" + releaseVersion,
        "TAG=" + tag,
        "FAMILY=" + releaseFamily,
        "IDS=" + string.Join(' ', ids),
        "PREVIOUS_TAG=" + previous,
        "LATEST=" + latest,
    ];
    foreach (string line in lines)
    {
        Console.WriteLine(line);
    }
    if (githubEnv is not null)
    {
        File.AppendAllLines(githubEnv, lines);
    }
    return 0;
}

int Projects()
{
    if (string.IsNullOrWhiteSpace(family))
    {
        return Usage("projects needs --family.");
    }

    (string key, string[] ids) = Family(family, "to pack; a release without families packs the whole solution");
    string[] chosen = FamilyProjects(key, ids);
    foreach (string project in chosen)
    {
        Console.WriteLine(project);
    }
    if (outFile is not null)
    {
        File.WriteAllLines(outFile, chosen);
    }
    return 0;
}

int Notes()
{
    if (string.IsNullOrWhiteSpace(family))
    {
        return Usage("notes needs --family.");
    }
    if (string.IsNullOrWhiteSpace(outFile))
    {
        return Usage("notes needs --out, the file the GitHub release reads its notes from.");
    }

    (string key, string[] ids) = Family(family, "to write notes for; a release without families takes GitHub's");
    string start = previousTag?.Trim() ?? "";
    string text;
    if (start.Length == 0)
    {
        text = $"The first release of the {key} family.\n";
    }
    else
    {
        string[] own = [.. ReleaseNotes.OwnPaths(repository, FamilyProjects(key, ids)), $"docs/{key}.md"];
        List<Commit> mine = ReleaseNotes.Log(repository, start, own, descendantsOnly: false);
        HashSet<string> listed = [.. mine.Select(c => c.Hash)];
        List<Commit> shared = [.. ReleaseNotes.Log(repository, start, ReleaseNotes.SharedInputs, descendantsOnly: true).Where(c => !listed.Contains(c.Hash))];
        text = ReleaseNotes.Render(key, start, mine, shared);
    }

    Console.Write(text);
    File.WriteAllText(outFile, text);
    return 0;
}

// The declared family's stem and ids; refused where there are no families, or not this one.
(string Key, string[] Ids) Family(string name, string purpose)
{
    IReadOnlyDictionary<string, string[]> families = Families.Read(repository)
        ?? throw new Refused($".github/repository.json declares no families, so there is no family {purpose}.");
    string key = name.Trim().ToLowerInvariant();
    if (!families.TryGetValue(key, out string[]? ids))
    {
        throw new Refused($"There is no family \"{name}\"; .github/repository.json declares {string.Join(", ", families.Keys)}.");
    }
    return (key, ids);
}

// The solution's project that packs each of the family's ids, by evaluated PackageId.
string[] FamilyProjects(string key, string[] ids)
{
    string[] solutions = [.. Directory.EnumerateFiles(repository)
        .Select(path => Path.GetFileName(path))
        .Where(f => f.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))];
    if (solutions.Length != 1)
    {
        throw new Refused($"Expected one solution at the repository root, found {solutions.Length}.");
    }

    (int listExit, string listed) = Shell.Run("dotnet", repository, "sln", solutions[0], "list");
    if (listExit != 0)
    {
        throw new Refused($"`dotnet sln {solutions[0]} list` failed: {listed.Trim()}");
    }

    string[] projects = [.. listed.Split('\n').Select(l => l.Trim()).Where(l => l.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))];
    Dictionary<string, List<string>> byId = new(StringComparer.OrdinalIgnoreCase);
    foreach ((string project, int exit, string output) in Evaluation.All(repository, projects, "-getProperty:PackageId", "-getProperty:IsPackable"))
    {
        JsonObject? properties = exit == 0 ? Evaluation.Properties(output) : null;
        if (properties is null)
        {
            throw new Refused($"{project} could not be evaluated, so whether it packs one of the {key} family's ids is unknown: {output.Trim()}");
        }
        string id = Evaluation.Text(properties["PackageId"]);
        if (Evaluation.Text(properties["IsPackable"]) == "true" && ids.Contains(id, StringComparer.OrdinalIgnoreCase))
        {
            (byId.TryGetValue(id, out List<string>? found) ? found : byId[id] = []).Add(project);
        }
    }

    List<string> chosen = [];
    foreach (string id in ids)
    {
        List<string> found = byId.GetValueOrDefault(id) ?? [];
        if (found.Count != 1)
        {
            throw new Refused(found.Count == 0
                ? $"No packable project in the solution has the PackageId {id}, which the {key} family names."
                : $"{found.Count} projects pack {id}: {string.Join(", ", found)}. One id, one project.");
        }
        chosen.Add(found[0]);
    }
    return [.. chosen];
}

static int Usage(string problem)
{
    Console.Error.WriteLine("release-target: " + problem);
    Console.Error.WriteLine("usage: release-target (resolve --event push --ref <tag> | resolve --event workflow_dispatch --version <v> [--family <f>]) [--github-env <file>] | projects --family <f> [--out <file>] | notes --family <f> [--previous <tag>] --out <file>; [--root DIR]");
    return 2;
}

/// <summary>A release this script will not resolve. Its message says why, and what to do instead.</summary>
sealed class Refused(string message) : Exception(message);

/// <summary>How a tag or a dispatch names a release, and what it must look like.</summary>
static class Release
{
    private static readonly Regex FamilyTag = new(@"^(?<family>[^/]+)/v(?<version>[^/]+)$");
    private static readonly Regex Shape = new(@"^(?<year>\d{4})\.(?<quarter>[1-4])\.(?<monthday>[1-9]\d{2,3})$");

    public static (string Family, string Version) FromTag(string tag, IReadOnlyDictionary<string, string[]>? families)
    {
        Match match = FamilyTag.Match(tag);
        if (families is null)
        {
            if (match.Success)
            {
                throw new Refused($"The tag {tag} names the family \"{match.Groups["family"].Value}\", but .github/repository.json declares no families. This repository releases from v<version>.");
            }
            if (!tag.StartsWith('v') || tag.Contains('/'))
            {
                throw new Refused($"The tag {tag} is not v<version>.");
            }
            return ("", tag[1..]);
        }

        if (!match.Success)
        {
            throw new Refused($"The tag {tag} names no family. This repository declares families ({string.Join(", ", families.Keys)}), and each releases from <family>/v<version>, such as {families.Keys.First()}/v{(tag.StartsWith('v') ? tag[1..] : "<version>")}.");
        }
        return (Known(match.Groups["family"].Value, families, "tag"), match.Groups["version"].Value);
    }

    public static (string Family, string Version) FromDispatch(string version, string family, IReadOnlyDictionary<string, string[]>? families)
    {
        if (version.Trim().Length == 0)
        {
            throw new Refused("A dispatch without a version is the credential preflight, which releases nothing.");
        }
        if (families is null)
        {
            if (family.Trim().Length > 0)
            {
                throw new Refused($"The dispatch names the family \"{family.Trim()}\", but .github/repository.json declares no families. Dispatch with the version alone.");
            }
            return ("", version.Trim());
        }

        if (family.Trim().Length == 0)
        {
            throw new Refused($"This repository declares families ({string.Join(", ", families.Keys)}), so a dispatch names the one it releases.");
        }
        return (Known(family.Trim().ToLowerInvariant(), families, "dispatch"), version.Trim());
    }

    // A tag's family is lower-case, so it has one spelling to look a previous tag up by. A dispatch's
    // is typed by hand and lower-cased before it gets here.
    private static string Known(string name, IReadOnlyDictionary<string, string[]> families, string source)
    {
        if (families.ContainsKey(name))
        {
            return name;
        }
        string? differently = families.Keys.FirstOrDefault(k => k.Equals(name, StringComparison.OrdinalIgnoreCase));
        throw new Refused(differently is not null
            ? $"The {source} names the family \"{name}\"; family tags are lower-case: {differently}/v<version>."
            : $"The {source} names the family \"{name}\", which .github/repository.json does not declare; it declares {string.Join(", ", families.Keys)}.");
    }

    public static void CheckVersion(string version)
    {
        Match match = Shape.Match(version);
        if (!match.Success)
        {
            throw new Refused($"The version {version} is not YYYY.Q.MMDD, such as 2026.4.1022: three parts, the quarter, and the month and day with the month's leading zero dropped.");
        }

        int year = int.Parse(match.Groups["year"].Value);
        int quarter = int.Parse(match.Groups["quarter"].Value);
        int monthDay = int.Parse(match.Groups["monthday"].Value);
        int month = monthDay / 100, day = monthDay % 100;
        if (month is < 1 or > 12 || day < 1 || day > DateTime.DaysInMonth(year, month))
        {
            throw new Refused($"The version {version} names no date: {monthDay} is not a month and a day.");
        }
        if (quarter != (month - 1) / 3 + 1)
        {
            throw new Refused($"The version {version} names quarter {quarter}, but {year}-{month:00}-{day:00} is in quarter {(month - 1) / 3 + 1}: {year}.{(month - 1) / 3 + 1}.{monthDay}.");
        }
    }

}

/// <summary>The repository's release tags, <c>v&lt;version&gt;</c> and <c>&lt;family&gt;/v&lt;version&gt;</c>, read with <c>git tag</c>.</summary>
static class Tags
{
    private static readonly Regex Tag = new(@"^(?:(?<family>[^/]+)/)?v(?<version>[^/]+)$");

    public static string[] List(string repository)
    {
        (int exit, string output) = Shell.Run("git", repository, "tag", "--list");
        if (exit != 0)
        {
            throw new Refused($"`git tag --list` failed, so the previous release is unknown: {output.Trim()}");
        }
        return [.. output.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0)];
    }

    /// <summary>
    /// The family's newest tag below <paramref name="version"/>, or empty for its first release. Where
    /// the family owns the bare tags ("bareTags"), a bare tag counts as one of its own.
    /// </summary>
    public static string Previous(string[] tags, string family, string version, bool ownsBareTags)
    {
        Version current = Version.Parse(version);
        return Parsed(tags)
            .Where(t => t.Family == family || (ownsBareTags && t.Family.Length == 0))
            .Where(t => t.Version < current)
            .OrderByDescending(t => t.Version)
            .Select(t => t.Tag)
            .FirstOrDefault() ?? "";
    }

    /// <summary>
    /// Whether a release at <paramref name="version"/> is the repository's newest, whichever family's
    /// (plans/00011 decision 8): no tag of any family, nor a bare one, has a higher version. A dispatch
    /// re-releasing an older version leaves "Latest" where it is.
    /// </summary>
    public static bool Latest(string[] tags, string version)
    {
        Version current = Version.Parse(version);
        return !Parsed(tags).Any(t => t.Version > current);
    }

    private static IEnumerable<(string Tag, string Family, Version Version)> Parsed(string[] tags)
    {
        foreach (string tag in tags)
        {
            Match match = Tag.Match(tag);
            if (match.Success && Version.TryParse(match.Groups["version"].Value, out Version? parsed))
            {
                yield return (tag, match.Groups["family"].Value, parsed);
            }
        }
    }
}

/// <summary>One commit as a release's notes list it.</summary>
sealed record Commit(string Hash, string Short, string Subject);

/// <summary>A family's release notes (plans/00011 decision 6), from <c>git log</c>.</summary>
static class ReleaseNotes
{
    /// <summary>
    /// The build inputs every package shares. A commit touching one changes every family's packages,
    /// as AssemblyQuality#17's friend grants did without touching its project.
    /// </summary>
    public static readonly string[] SharedInputs =
    [
        "Directory.Build.props",
        "Directory.Build.targets",
        "Directory.Packages.props",
        "global.json",
        "NuGet.config",
        ":(glob)AssemblyInfo.*.cs",
        "src/Directory.Build.props",
        "src/Directory.Build.targets",
    ];

    /// <summary>
    /// The directories of the family's projects, and of every project they reference that packs
    /// nothing, since its code ships inside theirs. A packable project of another family is not
    /// followed: `repo-conventions check` fails such a reference.
    /// </summary>
    public static IEnumerable<string> OwnPaths(string repository, string[] projects)
    {
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        // A level at a time, each level's projects evaluated together: the family's own projects
        // first, then whatever they reference, and so on.
        string[] level = [.. projects.Select(p => Path.GetFullPath(Path.Combine(repository, p))).Where(seen.Add)];
        bool isRoot = true;
        while (level.Length > 0)
        {
            List<string> next = [];
            foreach ((string project, int exit, string output) in Evaluation.All(repository, level, "-getProperty:IsPackable", "-getItem:ProjectReference"))
            {
                JsonObject? evaluated = exit == 0 ? Evaluation.Json(output) : null;
                if (evaluated is null)
                {
                    throw new Refused($"{project} could not be evaluated, so the paths of its family's notes are unknown: {output.Trim()}");
                }
                if (!isRoot && Evaluation.Text(evaluated["Properties"]?["IsPackable"]) == "true")
                {
                    continue;
                }

                yield return Path.GetRelativePath(repository, Path.GetDirectoryName(project)!).Replace('\\', '/');
                foreach (JsonNode? reference in evaluated["Items"]?["ProjectReference"]?.AsArray() ?? [])
                {
                    string path = Evaluation.Text(reference?["FullPath"]);
                    if (path.Length > 0 && seen.Add(path = Path.GetFullPath(path)))
                    {
                        next.Add(path);
                    }
                }
            }
            level = [.. next];
            isRoot = false;
        }
    }

    /// <summary>
    /// The commits after <paramref name="start"/> up to HEAD that touched <paramref name="paths"/>,
    /// merges aside, newest first. With <paramref name="descendantsOnly"/>, only those descending from
    /// <paramref name="start"/>: an imported history has root build files of the same names, and its
    /// commits are not this family's.
    /// </summary>
    public static List<Commit> Log(string repository, string start, string[] paths, bool descendantsOnly)
    {
        List<string> arguments = ["log", "--no-merges", "--format=%H%x1f%h%x1f%s"];
        if (descendantsOnly)
        {
            arguments.Add("--ancestry-path");
        }
        arguments.Add(start + "..HEAD");
        arguments.Add("--");
        arguments.AddRange(paths);

        (int exit, string output) = Shell.Run("git", repository, [.. arguments]);
        if (exit != 0)
        {
            throw new Refused($"`git log {start}..HEAD` failed, so the notes cannot be written: {output.Trim()}");
        }
        return [.. output.Split('\n')
            .Select(line => line.TrimEnd('\r').Split('\x1f'))
            .Where(parts => parts.Length == 3)
            .Select(parts => new Commit(parts[0], parts[1], parts[2]))];
    }

    public static string Render(string family, string start, List<Commit> own, List<Commit> shared)
    {
        if (own.Count == 0 && shared.Count == 0)
        {
            return $"No commit since {start} changed the {family} family's packages.\n";
        }

        System.Text.StringBuilder text = new();
        text.Append("## The ").Append(family).Append(" family since ").Append(start).Append('\n').Append('\n');
        foreach (Commit commit in own)
        {
            text.Append("- ").Append(commit.Subject).Append(" (").Append(commit.Short).Append(")\n");
        }
        if (own.Count == 0)
        {
            text.Append("Nothing of the family's own.\n");
        }
        if (shared.Count > 0)
        {
            text.Append('\n').Append("## The build every family shares\n").Append('\n');
            foreach (Commit commit in shared)
            {
                text.Append("- ").Append(commit.Subject).Append(" (").Append(commit.Short).Append(")\n");
            }
        }
        return text.ToString();
    }
}

/// <summary><c>"families": { "&lt;stem&gt;": ["&lt;id&gt;", …] }</c> in .github/repository.json, keyed by the lower-cased stem.</summary>
static class Families
{
    // A stem becomes part of a tag, so it is what a ref name may hold, lower-cased.
    private static readonly Regex Stem = new(@"^[a-z0-9]+(?:[._-][a-z0-9]+)*$");

    /// <summary>The declared families, or null when the repository declares none.</summary>
    public static IReadOnlyDictionary<string, string[]>? Read(string repository)
    {
        string path = Path.Combine(repository, ".github", "repository.json");
        if (!File.Exists(path))
        {
            return null;
        }

        JsonNode? node;
        try
        {
            node = JsonNode.Parse(File.ReadAllText(path))?["families"];
        }
        catch (JsonException error)
        {
            throw new Refused(".github/repository.json is not valid JSON: " + error.Message);
        }
        if (node is null)
        {
            return null;
        }
        if (node is not JsonObject declared || declared.Count == 0)
        {
            throw new Refused("\"families\" in .github/repository.json must map each family's stem to its ids.");
        }

        Dictionary<string, string[]> families = new(StringComparer.Ordinal);
        foreach ((string stem, JsonNode? ids) in declared)
        {
            string key = stem.ToLowerInvariant();
            if (!Stem.IsMatch(key))
            {
                throw new Refused($"The family \"{stem}\" cannot be part of a tag: letters and digits, separated by single '.', '-' or '_'.");
            }
            if (families.ContainsKey(key))
            {
                throw new Refused($"The family \"{stem}\" is declared twice.");
            }
            string[] list = ids is JsonArray array
                ? [.. array.Select(n => n is JsonValue value && value.TryGetValue(out string? id) ? id.Trim() : "")]
                : [];
            if (list.Length == 0 || list.Any(id => id.Length == 0))
            {
                throw new Refused($"The family \"{stem}\" must list its package ids.");
            }
            families[key] = list;
        }
        return families;
    }

    /// <summary>
    /// <c>"bareTags": "&lt;stem&gt;"</c> (plans/00011 decision 7): the family whose releases the
    /// repository's bare <c>v…</c> tags were, before it had families, or empty when none is named.
    /// </summary>
    public static string BareTags(string repository, IReadOnlyDictionary<string, string[]> families)
    {
        JsonNode? node = JsonNode.Parse(File.ReadAllText(Path.Combine(repository, ".github", "repository.json")))?["bareTags"];
        if (node is null)
        {
            return "";
        }
        string stem = node is JsonValue value && value.TryGetValue(out string? text) ? text.Trim().ToLowerInvariant() : "";
        if (!families.ContainsKey(stem))
        {
            throw new Refused($"\"bareTags\" in .github/repository.json is {node.ToJsonString()}, which is not a declared family; it declares {string.Join(", ", families.Keys)}.");
        }
        return stem;
    }
}

static class Lists
{
    /// <summary>A package list's ids, blank lines and <c>#</c> comments aside.</summary>
    public static string[] Read(string path) =>
        File.Exists(path)
            ? [.. File.ReadLines(path).Select(line => line.Trim()).Where(line => line.Length > 0 && !line.StartsWith('#'))]
            : [];
}

static class Evaluation
{
    /// <summary>The <c>Properties</c> object of <c>dotnet msbuild -getProperty</c>'s JSON, or null.</summary>
    public static JsonObject? Properties(string output) => Json(output)?["Properties"] as JsonObject;

    /// <summary><c>dotnet msbuild -getProperty</c>/<c>-getItem</c>'s whole JSON, or null.</summary>
    public static JsonObject? Json(string output)
    {
        int start = output.IndexOf('{');
        if (start < 0)
        {
            return null;
        }
        try
        {
            return JsonNode.Parse(output[start..]) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static string Text(JsonNode? node) => node is JsonValue value && value.TryGetValue(out string? text) ? text ?? "" : "";

    /// <summary>
    /// <c>dotnet msbuild -p:Configuration=Release</c> with <paramref name="queries"/> on each project,
    /// in the order given. The evaluations run together: each is a process of its own that spends most
    /// of a second starting, so one after another they cost a release a second a project.
    /// </summary>
    public static (string Project, int ExitCode, string Output)[] All(string repository, string[] projects, params string[] queries) =>
        [.. projects.AsParallel().AsOrdered().Select(project =>
        {
            (int exit, string output) = Shell.Run("dotnet", repository, ["msbuild", project, "-p:Configuration=Release", .. queries]);
            return (project, exit, output);
        })];
}

static class Shell
{
    public static (int ExitCode, string Output) Run(string file, string workingDirectory, params string[] arguments)
    {
        ProcessStartInfo start = new(file) { WorkingDirectory = workingDirectory, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }
        using Process process = Process.Start(start)!;
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        return (process.ExitCode, stdout.Result + stderr.Result);
    }
}
