using System;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using DM.Testing;
using Xunit;

namespace DM.Architecture.Tests;

/// <remarks>
/// PATTERNS draws the shape: {Concern}/ folders, Shared/{Concern}/ and one registration file. This
/// project only: for the others it is a decision, not a cleanup.
/// </remarks>
public class InfrastructureLayoutShould
{
    private const string Project = "DM.Infrastructure.Core";
    private const string Module = "CoreRegistrationExtensions.cs";


    /// <summary>
    /// A rule over an empty directory passes, so the tree is asserted before
    /// anything is asked of it.
    /// </summary>
    [Fact]
    public void FindTheConcernsOfTheSharedProject()
    {
        var project = Path.Combine(RepositoryLayout.Root, "src", Project);

        Directory.Exists(project).Should().BeTrue($"{Project} must still be where the rule looks");
        Directory.GetDirectories(project)
            .Select(path => new DirectoryInfo(path).Name)
            .Where(name => name is not ("bin" or "obj"))
            .Should().NotBeEmpty(
                "the project is a folder per technical concern, and finding none " +
                "would mean the rule below reads a tree that no longer exists");
    }

    [Fact]
    public void KeepNothingButItsModuleAtTheRoot()
    {
        var root = RepositoryLayout.Root;

        var offenders = Directory
            .GetFiles(Path.Combine(root, "src", Project), "*.cs", SearchOption.TopDirectoryOnly)
            .Where(file => Path.GetFileName(file) != Module)
            .Select(file => Path.GetRelativePath(root, file))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

        offenders.Should().BeEmpty(
            "the blueprint gives an infrastructure project {Concern}/ folders and one " +
            "module file; a class at the root belongs to no concern, and the next one " +
            "lands beside it because that is where the last one went");
    }
}
