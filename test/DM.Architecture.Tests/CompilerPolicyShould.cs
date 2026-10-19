using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using DM.Testing;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// Compiler policy is declared once for the whole solution, and a source file that
/// repeats it locally is either a no-op or a second policy nobody can see.
/// </summary>
/// <remarks>The props decide both for every project, so a local copy is a no-op that reads as a rule.</remarks>
public class CompilerPolicyShould
{
    /// <summary>A dotnet-version a workflow asks setup-dotnet to install.</summary>
    private static readonly Regex DotnetVersion = new(
        @"dotnet-version:\s*(?<version>[0-9]+\.[0-9]+\.[0-9x]+)", RegexOptions.Compiled);

    /// <summary>A file silencing the documentation diagnostic on its own account.</summary>
    private static readonly Regex DocumentationPragma = new(
        @"#pragma\s+warning\s+disable[^\r\n]*\b(?:CS)?1591\b", RegexOptions.Compiled);

    /// <summary>The documentation switch, written in a project that already inherits it.</summary>
    private const string DocumentationFileSetting =
        "<GenerateDocumentationFile>true</GenerateDocumentationFile>";

    /// <summary>A per-project grant of internals, which the props already give.</summary>
    private static readonly Regex InternalsGrant = new(
        @"<InternalsVisibleTo\s+Include=""([^""]+)""", RegexOptions.Compiled);

    /// <summary>The solution-wide grant, written in the props as an assembly attribute.</summary>
    private static readonly Regex SolutionWideGrant = new(
        @"<_Parameter1>\$\(AssemblyName\)\.([^<]+)</_Parameter1>", RegexOptions.Compiled);


    /// <summary>
    /// The documentation policy is one decision, and no file states it a second time.
    /// </summary>
    /// <remarks>The props suppress CS1591 for the whole solution, so a local suppression silences nothing.</remarks>
    [Fact]
    public void DeclareTheDocumentationPolicyInOnePlace()
    {
        var root = RepositoryLayout.Root;

        var withOwnPragma = Directory
            .EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(RepositoryFiles.IsAuthored)
            .Where(path => DocumentationPragma.IsMatch(File.ReadAllText(path)))
            .Select(path => Path.GetRelativePath(root, path))
            .ToList();

        withOwnPragma.Should().BeEmpty(
            "the props silence CS1591 for every project, so a pragma silences nothing and only " +
            "tells the next reader that this file answers to a documentation rule of its own");

        var withOwnSwitch = ProjectFiles(root)
            .Where(path => File
                .ReadAllText(path)
                .Contains(DocumentationFileSetting, StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(root, path))
            .ToList();

        withOwnSwitch.Should().BeEmpty(
            "the props already generate the XML file for every project, and a project that " +
            "repeats the value moves nothing while looking like the place it is decided");
    }

    /// <summary>
    /// Enum exhaustiveness stays a build error, and only the half that is
    /// actionable stays on.
    /// </summary>
    /// <remarks>
    /// CS8524 forces a `_ =>` arm that swallows CS8509, the one naming an unhandled member, so only
    /// CS8524 is suppressed.
    /// </remarks>
    [Fact]
    public void KeepEnumExhaustivenessEnforceable()
    {
        var props = File.ReadAllText(Path.Combine(RepositoryLayout.Root, "Directory.Build.props"));

        // Only the list is read: a code named in an explanation is not a suppression.
        var suppressed = System.Text.RegularExpressions.Regex
            .Matches(props, @"<NoWarn>(?<codes>[^<]*)</NoWarn>")
            .SelectMany(m => m.Groups["codes"].Value.Split(';'))
            .Select(code => code.Trim())
            .ToList();

        suppressed.Should().Contain("CS8524",
            "without it a switch covering every member still fails the build, and the only way " +
            "out is the discard arm this rule exists to make unnecessary");
        suppressed.Should().NotContain("CS8509",
            "CS8509 is the diagnostic that names the member nobody handled: silencing it turns " +
            "an added enum member back into a null on the wire");
    }

    /// <summary>
    /// The mapper diagnostics that stand in for a runtime configuration
    /// assertion stay build errors, nullability included.
    /// </summary>
    /// <remarks>
    /// RMG012, RMG020 and RMG068 stand for a configuration assertion and RMG090 for nullability;
    /// all ship below warning level.
    /// </remarks>
    [Fact]
    public void KeepTheMapperDiagnosticsAtErrorSeverity()
    {
        var editorConfig = File.ReadAllText(Path.Combine(RepositoryLayout.Root, ".editorconfig"));

        foreach (var diagnostic in new[] { "RMG012", "RMG020", "RMG068", "RMG090" })
        {
            editorConfig.Should().Contain(
                $"dotnet_diagnostic.{diagnostic}.severity = error",
                "{0} is only enforced by the line that raises it, and the diagnostic " +
                "ships quiet enough that lowering it fails nothing until production does",
                diagnostic);
        }
    }

    [Fact]
    public void NotRepeatTheSolutionWideNullableSettingInSourceFiles()
    {
        var root = RepositoryLayout.Root;

        var redundant = Directory
            .EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(RepositoryFiles.IsAuthored)
            .Where(path => File.ReadLines(path).Any(line => line.Trim() == "#nullable enable"))
            .Select(path => Path.GetRelativePath(root, path))
            .ToList();

        redundant.Should().BeEmpty(
            "Directory.Build.props turns nullable on for every project, so the directive changes " +
            "nothing and reads as a claim that the rest of the tree is not annotated");
    }

    /// <summary>
    /// The image build compiles the same sources under the same policy, so it needs
    /// the same policy files.
    /// </summary>
    /// <remarks>Without .editorconfig the image build compiles under a different policy than the solution.</remarks>
    [Fact]
    public void GiveTheImageBuildTheSamePolicyFiles()
    {
        var root = RepositoryLayout.Root;
        var copyLines = File
            .ReadAllLines(Path.Combine(root, "docker", "app.Dockerfile"))
            .Where(line => line.TrimStart().StartsWith("COPY ", StringComparison.Ordinal))
            .ToList();

        foreach (var policyFile in new[]
                 {
                     "Directory.Build.props",
                     "Directory.Packages.props",
                     ".editorconfig",
                 })
        {
            File.Exists(Path.Combine(root, policyFile)).Should()
                .BeTrue($"{policyFile} is part of the compiler policy");
            copyLines.Should().Contain(line => line.Contains(policyFile, StringComparison.Ordinal),
                $"the image build compiles under {policyFile} too, and a file left outside " +
                "the build context is simply absent rather than reported");
        }
    }

    /// <summary>
    /// Package versions live in the central files, all of them.
    /// </summary>
    /// <remarks>An override wins over the central value and is invisible from the file everyone edits.</remarks>
    [Fact]
    public void KeepEveryPackageVersionInTheCentralFiles()
    {
        var root = RepositoryLayout.Root;

        var withOverride = new[] { "src", "test" }
            .SelectMany(area => Directory.EnumerateFiles(
                Path.Combine(root, area), "*.csproj", SearchOption.AllDirectories))
            .Where(RepositoryFiles.IsAuthored)
            .Where(path => File.ReadAllText(path).Contains("VersionOverride", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(root, path))
            .ToList();

        withOverride.Should().BeEmpty(
            "a version pinned in a project file is a second source of truth that wins over the " +
            "central one without saying so");
    }

    /// <summary>
    /// The language of the solution is pinned in the props, and a project that names
    /// its own is the one place where the pin does not hold.
    /// </summary>
    /// <remarks>A project language of its own compiles locally and fails on the band CI pins.</remarks>
    [Fact]
    public void PinTheLanguageVersionInOnePlace()
    {
        var root = RepositoryLayout.Root;
        var offenders = ProjectFiles(root)
            .Where(path => File.ReadAllText(path).Contains("<LangVersion>", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(root, path))
            .ToList();

        offenders.Should().BeEmpty(
            "the accepted language is the one the build images have, and a project that " +
            "raises it above the pin compiles locally and fails where nobody can see why");
    }

    /// <summary>
    /// The SDK a developer compiles with is the one CI installs.
    /// </summary>
    /// <remarks>
    /// global.json takes no comments. The pin names the band the workflow installs, and the
    /// roll-forward stays inside one major.minor: every SDK brings new diagnostics.
    /// </remarks>
    [Fact]
    public void PinTheSdkToTheBandContinuousIntegrationInstalls()
    {
        var root = RepositoryLayout.Root;

        using var globalJson = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "global.json")));
        var sdk = globalJson.RootElement.GetProperty("sdk");
        var pinned = sdk.GetProperty("version").GetString();
        var rollForward = sdk.TryGetProperty("rollForward", out var declared)
            ? declared.GetString()
            : "latestPatch";

        rollForward.Should().BeOneOf(
            ["latestFeature", "feature", "latestPatch", "patch", "disable"],
            "a roll-forward that crosses a major or a minor hands the build to an SDK " +
            "CI does not install, and warnings are errors in this solution");

        var installed = InstalledSdkVersions(root);
        installed.Should().NotBeEmpty("the workflows install the SDK before building");

        var pinnedBand = Band(pinned!);
        installed.Should().AllSatisfy(version => Band(version).Should().Be(pinnedBand,
            "global.json and every setup-dotnet step name one band or the pin describes " +
            "a compiler nobody uses"));
    }

    /// <summary>Every dotnet-version the workflows ask setup-dotnet to install.</summary>
    private static IReadOnlyList<string> InstalledSdkVersions(string root) => Directory
        .EnumerateFiles(Path.Combine(root, ".github", "workflows"), "*.yml")
        .SelectMany(path => DotnetVersion.Matches(File.ReadAllText(path)))
        .Select(match => match.Groups["version"].Value)
        .ToList();

    /// <summary>major.minor — the part a feature-level roll-forward cannot leave.</summary>
    private static string Band(string version)
    {
        var parts = version.Split('.');
        parts.Length.Should().BeGreaterThanOrEqualTo(2, $"'{version}' should name a major and a minor");
        return parts[0] + "." + parts[1];
    }

    /// <summary>
    /// Internals are opened to assemblies this solution builds, and to no others.
    /// </summary>
    /// <remarks>The props open internals to $(AssemblyName).Tests, so a repeated grant changes nothing.</remarks>
    [Fact]
    public void GrantInternalsOnlyToAssembliesThatExist()
    {
        var root = RepositoryLayout.Root;
        var projects = ProjectFiles(root)
            .Select(path => Path.GetFileNameWithoutExtension(path))
            .ToHashSet(StringComparer.Ordinal);

        projects.Should().NotBeEmpty("the solution is built out of project files");

        var offenders = new List<string>();
        foreach (var path in ProjectFiles(root))
        {
            var owner = Path.GetFileNameWithoutExtension(path);
            foreach (Match match in InternalsGrant.Matches(File.ReadAllText(path)))
            {
                var granted = match.Groups[1].Value;
                var where = Path.GetRelativePath(root, path) + " -> " + granted;

                if (string.Equals(granted, owner + ".Tests", StringComparison.Ordinal))
                {
                    offenders.Add(where + " (the props already grant this one)");
                }
                else if (!projects.Contains(granted))
                {
                    offenders.Add(where + " (no project of that name)");
                }
            }
        }

        // The props grant to every project at once, so a suffix nothing is named
        // after opens seventeen assemblies to an assembly that cannot exist.
        var props = File.ReadAllText(Path.Combine(root, "Directory.Build.props"));
        foreach (Match match in SolutionWideGrant.Matches(props))
        {
            var suffix = match.Groups[1].Value;
            if (!projects.Any(project => project.EndsWith("." + suffix, StringComparison.Ordinal)))
            {
                offenders.Add("Directory.Build.props -> $(AssemblyName)." + suffix +
                              " (no project of this solution is named after that suffix)");
            }
        }

        offenders.Should().BeEmpty(
            "a grant to an assembly nothing in this tree builds describes a solution " +
            "that does not exist, and the reader takes it for a description of this one");
    }

    /// <summary>
    /// Nothing here is compiled against an older shape of anything here.
    /// </summary>
    /// <remarks>Every consumer is rebuilt from the same sources, so a forwarder serves nobody.</remarks>
    [Fact]
    public void ForwardNoTypeBetweenAssembliesOfThisSolution()
    {
        var root = RepositoryLayout.Root;
        var offenders = Directory
            .EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(RepositoryFiles.IsAuthored)
            .Where(path => File.ReadAllText(path).Contains("TypeForwardedTo", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(root, path))
            .ToList();

        offenders.Should().BeEmpty(
            "a forwarder serves a binary that cannot be rebuilt, and there is none here, " +
            "so it only tells the next reader that this assembly ships as a package");
    }

    private static IEnumerable<string> ProjectFiles(string root) =>
        new[] { "src", "test" }
            .SelectMany(folder => Directory.EnumerateFiles(
                Path.Combine(root, folder), "*.csproj", SearchOption.AllDirectories))
            .Where(RepositoryFiles.IsAuthored);
}
