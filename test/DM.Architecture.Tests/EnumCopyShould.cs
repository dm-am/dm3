using System;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// The words a person reads live in one dictionary, and it is not an attribute
/// on a domain enum.
/// </summary>
/// <remarks>
/// Ten enums carried 43 Description attributes holding Russian labels, and nothing
/// read a single one: the extension that could was never called, in src or in test.
/// They were a second dictionary of words the client already owns, and the two had
/// drifted — Active read "Активен" on the server while the screen said "Идет игра".
/// A dictionary nobody renders cannot be corrected by looking at the site, so the
/// rule is that the server keeps none.
///
/// The check is textual for the reason the refusal check is: an attribute gets
/// copied from the member above it, and no type stands in the way.
/// </remarks>
public class EnumCopyShould
{
    private static readonly string[] BuildOutput = ["bin", "obj", "node_modules", "coverage", "dist"];

    [Fact]
    public void KeepNoDisplayLabelsOnTheServer()
    {
        var root = RepositoryRoot;

        var offenders = RepositoryFiles.CsharpFiles(Path.Combine(root.FullName, "src"), BuildOutput)
            .Where(source => File.ReadAllText(source).Contains("[Description(", StringComparison.Ordinal))
            .Select(source => Path.GetRelativePath(root.FullName, source).Replace('\\', '/'))
            .ToList();

        offenders.Should().BeEmpty(
            "a label the server never renders cannot be corrected by looking at the site, " +
            "and the one the client renders had already drifted away from it");
    }

    private static DirectoryInfo RepositoryRoot => DM.Testing.RepositoryLayout.RootDirectory;
}
