using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// Compiler policy is declared once for the whole solution, and a source file that
/// repeats it locally is either a no-op or a second policy nobody can see.
/// </summary>
/// <remarks>
/// Both rules guard the same failure from opposite sides. A file-level nullable
/// directive reads as a claim that the rest of the tree is not annotated, which is
/// the reverse of the truth, and nothing in the file tells the reader it changes
/// nothing. A per-file CS1591 pragma says the same about documentation: the props
/// suppress that diagnostic for every project, so the pragma silences nothing and
/// leaves the next reader believing this one file answers to a rule the others do
/// not. Neither is visible to the compiler, which is why they are
/// asserted against the sources rather than against the loaded assemblies.
/// </remarks>
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

    private static string RepositoryRoot => DM.Testing.RepositoryLayout.Root;

    /// <summary>
    /// The documentation policy is one decision, and no file states it a second time.
    /// </summary>
    /// <remarks>
    /// CS1591 was an error for years, and the exemptions grew wherever the requirement
    /// hurt most: a pragma at the top of a file, a severity line for a folder, a NoWarn
    /// inside the one project whose XML is ever read. The props suppress the diagnostic
    /// for the whole solution now, so each of those silences nothing while still reading
    /// as a rule that holds there and not elsewhere. A suppression that changes nothing
    /// is also the hardest kind to remove later: nobody can tell what it was holding up.
    /// </remarks>
    [Fact]
    public void DeclareTheDocumentationPolicyInOnePlace()
    {
        var root = RepositoryRoot;

        var withOwnPragma = Directory
            .EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(IsAuthored)
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
    /// CS8524 fires on the cast of an integer no member declares, so a switch
    /// listing every member of an enum still warns and, under
    /// TreatWarningsAsErrors, cannot be compiled without a `_ =>` arm. That arm
    /// then swallows CS8509 as well — the one that names a member nobody
    /// handled — and three conversions in the account layer carried exactly that
    /// pair, so a reason added to a domain enum reached the client as null.
    /// Suppressing CS8524 while leaving CS8509 on is what makes the compiler the
    /// guard; suppressing CS8509 too would put it straight back.
    /// </remarks>
    [Fact]
    public void KeepEnumExhaustivenessEnforceable()
    {
        var props = File.ReadAllText(Path.Combine(RepositoryRoot, "Directory.Build.props"));

        // The list of suppressions is read rather than the whole file: the reason
        // for a decision lives next to it as a comment, and a code named in an
        // explanation is not a suppression.
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
    /// RMG012 and RMG020 replaced AutoMapper's AssertConfigurationIsValid and
    /// RMG068 guards the queryable projections; RMG090 is the nullability half
    /// and was added after it cost the site every game page it had. A mapping
    /// whose result is nullable feeding a member declared non-nullable does not
    /// carry the null - Mapperly throws on it, so one authorless row answered
    /// 500 for a whole response. All four ship below warning level, which under
    /// TreatWarningsAsErrors means invisible, so each is worth exactly the line
    /// in .editorconfig that raises it and nothing without it.
    /// </remarks>
    [Fact]
    public void KeepTheMapperDiagnosticsAtErrorSeverity()
    {
        var editorConfig = File.ReadAllText(Path.Combine(RepositoryRoot, ".editorconfig"));

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
        var root = RepositoryRoot;

        var redundant = Directory
            .EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(IsAuthored)
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
    /// <remarks>
    /// The props travel with the solution and were copied from the start;
    /// .editorconfig was not, and the day a CS1591 exemption moved into it the
    /// image build began failing on warnings no developer could see. The failure
    /// named a source file and a missing XML comment, which is the one thing that
    /// was not wrong — and it took every image down at once, so a green solution
    /// still shipped nothing. That exemption is gone, since the props now suppress
    /// the diagnostic for every project, and the file still travels: it is where a
    /// per-folder severity goes when the next one is needed, and its absence fails
    /// the build with a message that names everything except its own cause.
    /// </remarks>
    [Fact]
    public void GiveTheImageBuildTheSamePolicyFiles()
    {
        var root = RepositoryRoot;
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
    /// <remarks>
    /// The integration test project pinned four packages with VersionOverride.
    /// Two repeated the central value, so raising it there did nothing — the
    /// override wins — and two bypassed central management entirely, which let
    /// three packages of one family drift apart silently. An override is
    /// invisible from the file everyone edits when bumping a version.
    /// </remarks>
    [Fact]
    public void KeepEveryPackageVersionInTheCentralFiles()
    {
        var root = RepositoryRoot;

        var withOverride = new[] { "src", "test" }
            .SelectMany(area => Directory.EnumerateFiles(
                Path.Combine(root, area), "*.csproj", SearchOption.AllDirectories))
            .Where(IsAuthored)
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
    /// <remarks>
    /// Two projects carried the value "preview". CI pins the SDK band (8.0.x at
    /// the time) and the images build on the same band, so a feature of the next
    /// language compiled on a developer machine and stopped CI with an error
    /// that names a feature and no
    /// cause: the one failure the pin was written against, reintroduced by two lines
    /// that read as harmless repetition next to two properties which really were.
    /// </remarks>
    [Fact]
    public void PinTheLanguageVersionInOnePlace()
    {
        var root = RepositoryRoot;
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
    /// global.json takes no comments, so the reason lives here, and it has two
    /// halves.
    ///
    /// The band: the pin is worth something only while it names what CI
    /// installs. Raise the workflow to a newer SDK and leave the pin behind, and
    /// the file that looks like the source of truth stops describing anything.
    /// Matched against the workflow rather than against a number written here,
    /// so this rule cannot go stale on its own.
    ///
    /// The roll-forward: it was latestMajor, which accepts any SDK from the
    /// eighth upwards, and the machine carried only a newer one — so the whole
    /// solution compiled with a compiler CI does not have, while warnings are
    /// errors here without exception. Every SDK brings diagnostics the last one
    /// did not, so neither direction of that divergence is safe to assume away.
    /// Anything below "feature" stays inside one major.minor; the rest of the
    /// vocabulary crosses it, which is the whole defect.
    /// </remarks>
    [Fact]
    public void PinTheSdkToTheBandContinuousIntegrationInstalls()
    {
        var root = RepositoryRoot;

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
    /// <remarks>
    /// The props open them to $(AssemblyName).Tests for every project, so a project
    /// that repeats the same grant emits the attribute twice and changes nothing. The
    /// grants that named nothing were worse than useless: they described a layout the
    /// tree does not have, and one of them read as evidence that the kernel has a test
    /// project.
    /// </remarks>
    [Fact]
    public void GrantInternalsOnlyToAssembliesThatExist()
    {
        var root = RepositoryRoot;
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
    /// <remarks>
    /// A type forwarder earns its place when somebody else's binary was built against
    /// the namespace a type has left. Every consumer in this repository is rebuilt from
    /// the same sources, so the file forwarded for nobody while its comment claimed an
    /// outside consumer, which is exactly why it survived every cleanup.
    /// </remarks>
    [Fact]
    public void ForwardNoTypeBetweenAssembliesOfThisSolution()
    {
        var root = RepositoryRoot;
        var offenders = Directory
            .EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(IsAuthored)
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
            .Where(IsAuthored);

    private static bool IsAuthored(string path) =>
        !path
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment => segment is "obj" or "bin" or "node_modules");
}
