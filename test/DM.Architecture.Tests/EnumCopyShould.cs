using System;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using DM.Testing;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// The words a person reads live in one dictionary, and it is not an attribute
/// on a domain enum.
/// </summary>
/// <remarks>The client owns the words; textual, because an attribute is copied from the member above.</remarks>
public class EnumCopyShould
{
    private static readonly string[] BuildOutput = ["bin", "obj", "node_modules", "coverage", "dist"];

    [Fact]
    public void KeepNoDisplayLabelsOnTheServer()
    {
        var root = RepositoryLayout.RootDirectory;

        var offenders = RepositoryFiles.Files(Path.Combine(root.FullName, "src"), "*.cs", BuildOutput)
            .Where(source => File.ReadAllText(source).Contains("[Description(", StringComparison.Ordinal))
            .Select(source => Path.GetRelativePath(root.FullName, source).Replace('\\', '/'))
            .ToList();

        offenders.Should().BeEmpty(
            "a label the server never renders cannot be corrected by looking at the site, " +
            "and the one the client renders had already drifted away from it");
    }
}
