using System;
using System.IO;
using System.Linq;
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

    private static bool IsAuthored(string path) =>
        !path
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment => segment is "obj" or "bin" or "node_modules");
}
