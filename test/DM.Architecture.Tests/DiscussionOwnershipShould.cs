using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// Reading a discussion is written once, whatever owns the comments.
/// </summary>
/// <remarks>
/// Four API services answered the same question — the comments of a blog, of a
/// publication, of a topic, of a game — with the same forty lines copied four
/// times, and the copies had already parted: the publication one shipped
/// without the blacklist filter, so a reader who hid an author saw him again
/// under a publication and nowhere else. Nothing failed. The endpoint answered
/// 200 with one comment more than the reader had asked to see.
///
/// The check is textual because the alternative is not expressible in types:
/// each surface has its own service interface and its own query type, and the
/// only thing they share is the answer they build. What the next surface gets
/// copied from is the source of the previous one.
/// </remarks>
public class DiscussionOwnershipShould
{
    /// <summary>
    /// The two steps a copied discussion reader carries: building the response,
    /// and asking which authors the reader hid. The second is the one the fourth
    /// copy left out.
    /// </summary>
    private static readonly string[] SharedSteps =
    [
        "new DiscussionResponse(",
        "GetBlockedUserIdsIfFlagEnabledAsync",
    ];

    private const string Owner = "CommentReading.cs";

    private static DirectoryInfo RepositoryRoot => DM.Testing.RepositoryLayout.RootDirectory;

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void BeAssembledInOnePlace(int step)
    {
        var root = RepositoryRoot;
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
            if (File.ReadAllText(source).Contains(SharedSteps[step], StringComparison.Ordinal))
            {
                carriers.Add(Path.GetFileName(source));
            }
        }

        carriers.Should().Equal([Owner],
            $"'{SharedSteps[step]}' belongs to the one reader of a discussion; a second file " +
            "carrying it is the copy whose next revision forgets half the behaviour");
    }
}
