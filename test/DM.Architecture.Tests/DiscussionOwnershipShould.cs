using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using DM.Testing;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// Reading a discussion is written once, whatever owns the comments.
/// </summary>
/// <remarks>Textual: each surface has its own service interface and query type, so types cannot say it.</remarks>
public class DiscussionOwnershipShould
{
    /// <summary>
    /// The step a copied discussion reader carries: asking which authors the reader hid.
    /// </summary>
    private const string SharedStep = "GetBlockedUserIdsIfFlagEnabledAsync";

    private const string Owner = "CommentReading.cs";


    [Fact]
    public void BeAssembledInOnePlace()
    {
        var root = RepositoryLayout.RootDirectory;
        var separator = Path.DirectorySeparatorChar;

        var sources = Directory
            .GetFiles(Path.Combine(root.FullName, "src", "DM.Web.API"), "*.cs",
                SearchOption.AllDirectories)
            .Where(path =>
                !path.Contains($"{separator}obj{separator}", StringComparison.Ordinal) &&
                !path.Contains($"{separator}bin{separator}", StringComparison.Ordinal))
            .ToList();

        sources.Should().NotBeEmpty("the HTTP host is a C# project");

        var carriers = new List<string>();
        foreach (var source in sources)
        {
            if (File.ReadAllText(source).Contains(SharedStep, StringComparison.Ordinal))
            {
                carriers.Add(Path.GetFileName(source));
            }
        }

        carriers.Should().Equal([Owner],
            $"'{SharedStep}' belongs to the one reader of a discussion; a second file " +
            "carrying it is the copy whose next revision forgets it");
    }
}
