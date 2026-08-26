using System;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// The shared infrastructure project is a set of technical concerns and the
/// module that registers them, and nothing else sits at its root.
/// </summary>
/// <remarks>
/// PATTERNS draws the shape: {Concern}/ folders, Shared/{Concern}/ for the
/// kernel contracts, and one registration file. DM.Infrastructure.Core kept five
/// files outside any of them — the clock, the identifier factory, the random
/// generator, the cursor encoder and a file of type forwarders — and each one
/// was a precedent for the next file to land there, until the root is where
/// things go when nobody has decided what they are.
///
/// This project only, and deliberately so. Mail, Messaging and Persistence keep
/// files at their roots too, but the answer for those is not obviously the same:
/// DmDbContext is the centre of its project rather than a stray, and whether the
/// blueprint should bend for it is a decision rather than a cleanup. Naming one
/// project keeps the rule true instead of listing the debt of three others as if
/// it were allowed.
///
/// A rule over the tree rather than over the assemblies: a folder is not a thing
/// the IL knows about.
/// </remarks>
public class InfrastructureLayoutShould
{
    private const string Project = "DM.Infrastructure.Core";
    private const string Module = "CoreRegistrationExtensions.cs";

    private static string RepositoryRoot => DM.Testing.RepositoryLayout.Root;

    /// <summary>
    /// A rule over an empty directory passes, so the tree is asserted before
    /// anything is asked of it.
    /// </summary>
    [Fact]
    public void FindTheConcernsOfTheSharedProject()
    {
        var project = Path.Combine(RepositoryRoot, "src", Project);

        Directory.Exists(project).Should().BeTrue($"{Project} must still be where the rule looks");
        Directory.GetDirectories(project)
            .Select(path => new DirectoryInfo(path).Name)
            .Where(name => name is not ("bin" or "obj"))
            .Should().HaveCountGreaterThanOrEqualTo(8,
                "the project is a folder per technical concern, and finding a handful " +
                "would mean the rule below reads a tree that no longer exists");
    }

    [Fact]
    public void KeepNothingButItsModuleAtTheRoot()
    {
        var root = RepositoryRoot;

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
