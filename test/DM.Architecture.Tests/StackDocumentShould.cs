using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using AwesomeAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// The stack document names what the manifests carry, at the versions they carry,
/// and nothing they do not.
/// </summary>
/// <remarks>
/// The chapter on the stack in the development document described a stack the
/// tree no longer had: a second database, a container, a mapper and a mocking
/// library that had all been removed, at versions nobody installed. A list of
/// technologies is copied from the manifests once and then drifts from them with
/// every upgrade, so the copy is held against its source.
///
/// A name is the text between backticks in the DM3 section, and its version is the
/// token right after it when that token carries a digit. A name ending in ".*" or
/// "/*" is a group and covers every manifest name it is a prefix of, and the stem
/// itself. A list item is read whole, its wrapped lines joined to it, so a version
/// that wraps onto the next line is still the token after the name. An item that
/// says "в плане" names what is not there yet and is not read.
/// The introduction above the DM3 heading is not read either.
///
/// What the manifests are: the central package versions, the client's package.json
/// with versions resolved from its lock file and the engines it pins, the images of
/// the compose files, the base images of the Dockerfiles, the actions of the
/// workflows, the SDK of global.json and the language version of the build.
/// </remarks>
public class StackDocumentShould
{
    private const string Dm3Heading = "## DM3";

    private const string Planned = "в плане";

    private static readonly Regex Named = new(
        @"`(?<name>[^`]+)`(?: (?<version>[0-9A-Za-z.+_-]*\d[0-9A-Za-z.+_-]*))?",
        RegexOptions.Compiled);

    private static readonly Regex Image = new(
        @"^\s*image:\s*['""]?(?<ref>[^'""\s]+)", RegexOptions.Compiled | RegexOptions.Multiline);

    private static readonly Regex BaseImage = new(
        @"^FROM\s+(?<ref>\S+)", RegexOptions.Compiled | RegexOptions.Multiline);

    private static readonly Regex Action = new(
        @"uses:\s*(?<name>[^@\s'""]+)@(?<version>[^\s'""]+)", RegexOptions.Compiled);

    private sealed record Entry(string Name, string? Version, string Source);

    private sealed record Mention(string Name, string? Version);

    private static string Root => DM.Testing.RepositoryLayout.Root;

    [Fact]
    public void NameEveryManifestEntry()
    {
        var mentions = Mentions();
        var unnamed = Manifests()
            .Where(entry => !mentions.Any(mention => Covers(mention.Name, entry.Name)))
            .Select(entry => $"{entry.Source}: {entry.Name}")
            .Distinct()
            .ToList();

        unnamed.Should().HaveCount(0,
            "every package, image and action the build installs has a line in docs/STACK.md, " +
            "by its own name in backticks or by a group such as `Microsoft.Extensions.*`");
    }

    [Fact]
    public void WriteEveryVersionAsTheManifestHasIt()
    {
        var manifests = Manifests();
        var wrong = new List<string>();
        foreach (var mention in Mentions().Where(mention => mention.Version is not null))
        {
            var covered = manifests.Where(entry => Covers(mention.Name, entry.Name)).ToList();
            var holds = IsGroup(mention.Name)
                ? covered.All(entry => entry.Version == mention.Version)
                : covered.Count == 0 || covered.Any(entry => entry.Version == mention.Version);
            if (!holds)
            {
                var actual = string.Join(", ", covered.Select(entry => $"{entry.Name} {entry.Version ?? "untagged"}").Distinct());
                wrong.Add($"`{mention.Name}` {mention.Version}: the manifests carry {actual}");
            }
        }

        wrong.Should().HaveCount(0,
            "a version written next to a name is the one the manifests install; a group's " +
            "version holds for every member, so a group whose members differ carries none");
    }

    [Fact]
    public void NameOnlyWhatTheManifestsCarry()
    {
        var manifests = Manifests();
        var invented = Mentions()
            .Where(mention => !manifests.Any(entry => Covers(mention.Name, entry.Name)))
            .Select(mention => mention.Name)
            .Distinct()
            .ToList();

        invented.Should().HaveCount(0,
            "a name in backticks in the DM3 section is a promise that the build installs it; " +
            "what is only planned says \"" + Planned + "\" on its line, and other words go without backticks");
    }

    [Theory]
    [InlineData("Microsoft.Extensions.*", "Microsoft.Extensions.Options", true)]
    [InlineData("Swashbuckle.AspNetCore.*", "Swashbuckle.AspNetCore", true)]
    [InlineData("@types/*", "@types/node", true)]
    [InlineData("Serilog", "Serilog.AspNetCore", false)]
    [InlineData("Microsoft.Extensions.*", "Microsoft.ExtensionsX", false)]
    public void CoverAManifestNameByExactNameOrByGroup(string mention, string name, bool covers) =>
        Covers(mention, name).Should().Be(covers);

    [Theory]
    [InlineData("- `vue` 3.5.43, фреймворк.", "vue", "3.5.43")]
    [InlineData("- `chrislusf/seaweedfs` 4.48, хранилище.", "chrislusf/seaweedfs", "4.48")]
    [InlineData("- `@types/*`, типы.", "@types/*", null)]
    [InlineData("- `dm-seeder` и образ сида", "dm-seeder", null)]
    [InlineData("- `Npgsql.EntityFrameworkCore.PostgreSQL`\n  10.0.3: провайдер.", "Npgsql.EntityFrameworkCore.PostgreSQL", "10.0.3")]
    public void ReadTheVersionRightAfterTheName(string text, string name, string? version)
    {
        var mention = Items(text.Split('\n')).SelectMany(Read).Single();
        mention.Name.Should().Be(name);
        mention.Version.Should().Be(version);
    }

    private static bool IsGroup(string mention) => mention.EndsWith(".*", StringComparison.Ordinal) || mention.EndsWith("/*", StringComparison.Ordinal);

    private static bool Covers(string mention, string name) => IsGroup(mention)
        ? name.StartsWith(mention[..^1], StringComparison.Ordinal) || name == mention[..^2]
        : name == mention;

    private static List<Mention> Mentions()
    {
        var lines = File.ReadAllLines(Path.Combine(Root, "docs", "STACK.md"));
        var start = Array.IndexOf(lines, Dm3Heading);
        start.Should().BeGreaterThan(0, "docs/STACK.md has an introduction and then a \"" + Dm3Heading + "\" section");
        return Items(lines.Skip(start + 1))
            .Where(item => !item.Contains(Planned, StringComparison.Ordinal))
            .SelectMany(Read)
            .ToList();
    }

    /// <summary>Lines with every indented continuation joined to the line it continues.</summary>
    private static IEnumerable<string> Items(IEnumerable<string> lines)
    {
        string? item = null;
        foreach (var line in lines)
        {
            if (item is not null && line.StartsWith("  ", StringComparison.Ordinal) && line.Trim().Length > 0)
            {
                item += " " + line.Trim();
                continue;
            }

            if (item is not null)
            {
                yield return item;
            }

            item = line;
        }

        if (item is not null)
        {
            yield return item;
        }
    }

    private static IEnumerable<Mention> Read(string line) => Named.Matches(line).Select(match => new Mention(
        match.Groups["name"].Value,
        match.Groups["version"].Success ? match.Groups["version"].Value.TrimEnd('.') : null));

    private static List<Entry> Manifests() =>
    [
        .. CentralPackages(),
        .. ClientPackages(),
        .. Images(),
        .. Actions(),
        .. Toolchain(),
    ];

    private static IEnumerable<Entry> CentralPackages() => XDocument
        .Load(Path.Combine(Root, "Directory.Packages.props"))
        .Descendants("PackageVersion")
        .Select(package => new Entry(
            (string)package.Attribute("Include")!, (string?)package.Attribute("Version"), "Directory.Packages.props"));

    private static IEnumerable<Entry> ClientPackages()
    {
        var client = Path.Combine(Root, "src", "DM.Web.Client");
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(client, "package.json")));
        using var lockFile = JsonDocument.Parse(File.ReadAllText(Path.Combine(client, "package-lock.json")));
        var resolved = lockFile.RootElement.GetProperty("packages");
        var entries = new List<Entry>();
        foreach (var section in new[] { "dependencies", "devDependencies" })
        {
            foreach (var package in manifest.RootElement.GetProperty(section).EnumerateObject())
            {
                var version = resolved.TryGetProperty("node_modules/" + package.Name, out var locked)
                    ? locked.GetProperty("version").GetString()
                    : null;
                entries.Add(new Entry(package.Name, version, "package.json " + section));
            }
        }

        entries.AddRange(manifest.RootElement.GetProperty("engines").EnumerateObject()
            .Select(engine => new Entry(engine.Name, engine.Value.GetString(), "package.json engines")));
        return entries;
    }

    private static IEnumerable<Entry> Images()
    {
        var composes = Directory.GetFiles(Path.Combine(Root, "docker"), "*.yml")
            .SelectMany(file => Image.Matches(File.ReadAllText(file)).Select(match => (file, match)));
        var dockerfiles = Directory.GetFiles(Path.Combine(Root, "docker"), "*Dockerfile")
            .Concat(Directory.GetDirectories(Path.Combine(Root, "src"))
                .Select(project => Path.Combine(project, "Dockerfile"))
                .Where(File.Exists))
            .SelectMany(file => BaseImage.Matches(File.ReadAllText(file)).Select(match => (file, match)));
        return composes.Concat(dockerfiles).Select(found => Split(
            found.match.Groups["ref"].Value, Path.GetRelativePath(Root, found.file).Replace('\\', '/')));
    }

    /// <summary>An image reference at its tag; a tag set by a variable is no version.</summary>
    /// <remarks>A digest after the tag pins it and is not the version a reader writes down.</remarks>
    private static Entry Split(string reference, string source)
    {
        var digest = reference.IndexOf('@');
        if (digest >= 0)
        {
            reference = reference[..digest];
        }

        var colon = reference.LastIndexOf(':');
        if (colon < 0 || colon < reference.LastIndexOf('/'))
        {
            return new Entry(reference, null, source);
        }

        var tag = reference[(colon + 1)..];
        var name = reference[..colon];
        if (name.Contains("${", StringComparison.Ordinal))
        {
            name = name[..name.IndexOf("${", StringComparison.Ordinal)].TrimEnd(':');
        }

        return new Entry(name, tag.Contains('$') || tag.Contains('}') ? null : tag, source);
    }

    private static IEnumerable<Entry> Actions() => Directory
        .GetFiles(Path.Combine(Root, ".github", "workflows"), "*.yml")
        .SelectMany(file => Action.Matches(File.ReadAllText(file)).Select(match => new Entry(
            match.Groups["name"].Value, match.Groups["version"].Value, ".github/workflows/" + Path.GetFileName(file))));

    private static IEnumerable<Entry> Toolchain()
    {
        using var global = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "global.json")));
        yield return new Entry(".NET SDK", global.RootElement.GetProperty("sdk").GetProperty("version").GetString(), "global.json");
        var language = XDocument.Load(Path.Combine(Root, "Directory.Build.props")).Descendants("LangVersion").Single().Value;
        yield return new Entry("C#", language, "Directory.Build.props");
    }
}
