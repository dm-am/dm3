using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FluentAssertions;
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
/// nothing. A per-file CS1591 pragma is worse: it leaves one folder split between
/// two documentation policies, so the author of the next entity has to guess which
/// one is in force. Neither is visible to the compiler, which is why they are
/// asserted against the sources rather than against the loaded assemblies.
/// </remarks>
public class CompilerPolicyShould
{
    private const string EntitiesSection = "[src/DM.Infrastructure.Persistence/Entities/**.cs]";
    private const string DocumentationExemption = "dotnet_diagnostic.CS1591.severity = none";

    /// <summary>A per-project grant of internals, which the props already give.</summary>
    private static readonly Regex InternalsGrant = new(
        @"<InternalsVisibleTo\s+Include=""([^""]+)""", RegexOptions.Compiled);

    /// <summary>The solution-wide grant, written in the props as an assembly attribute.</summary>
    private static readonly Regex SolutionWideGrant = new(
        @"<_Parameter1>\$\(AssemblyName\)\.([^<]+)</_Parameter1>", RegexOptions.Compiled);

    /// <summary>
    /// Walks up from the test binary to the repository root. The sources are not
    /// copied to the output directory, and copying them would let this assert
    /// against a stale snapshot.
    /// </summary>
    private static string RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null &&
                   !(Directory.Exists(Path.Combine(directory.FullName, "src")) &&
                     Directory.Exists(Path.Combine(directory.FullName, "test"))))
            {
                directory = directory.Parent;
            }

            directory.Should().NotBeNull("the repository root must be above the test binary");
            return directory!.FullName;
        }
    }

    [Fact]
    public void DeclareTheEntityDocumentationExemptionInOnePlace()
    {
        var root = RepositoryRoot;
        var editorConfig = File.ReadAllText(Path.Combine(root, ".editorconfig"));

        editorConfig.Should().Contain(EntitiesSection,
            "the exemption is declared for the folder, so a new entity file inherits it");
        editorConfig.Should().Contain(DocumentationExemption,
            "without the severity line the exemption lives nowhere and the build stops on CS1591");

        var withOwnPragma = Directory
            .EnumerateFiles(
                Path.Combine(root, "src", "DM.Infrastructure.Persistence", "Entities"),
                "*.cs",
                SearchOption.AllDirectories)
            .Where(path => File
                .ReadAllText(path)
                .Contains("#pragma warning disable CS1591", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(root, path))
            .ToList();

        withOwnPragma.Should().BeEmpty(
            "a per-file pragma is a second documentation policy in a folder that already has one");
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

        // Читается список подавленных, а не весь файл: обоснование решения живет
        // рядом с ним комментарием, и упоминание кода в объяснении это не подавление.
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
    /// .editorconfig was not, and the day the CS1591 exemption moved into it the
    /// image build began failing on warnings no developer could see. The failure
    /// named a source file and a missing XML comment, which is the one thing that
    /// was not wrong — and it took every image down at once, so a green solution
    /// still shipped nothing.
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
    /// Two projects carried the value "preview". CI pins the SDK to 8.0.x and the
    /// images build on sdk:8.0, so a feature of the next language compiled on a
    /// developer machine and stopped CI with an error that names a feature and no
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
