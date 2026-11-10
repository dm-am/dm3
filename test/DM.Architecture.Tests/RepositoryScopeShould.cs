using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using DM.Testing;
using Xunit;

namespace DM.Architecture.Tests;

/// <remarks>
/// An unused parameter is legal, so a signature promising a scoped read is checked here: a parameter
/// that names the reader appears in its body. Whether the scope is right is a test over data.
/// </remarks>
public class RepositoryScopeShould
{
    private static readonly string[] ReaderParameters = { "userId", "viewerId", "readerId", "currentUserId" };

    private static IEnumerable<string> PersistenceSources() => Directory
        .EnumerateFiles(Path.Combine(RepositoryLayout.Root, "src", "DM.Infrastructure.Persistence", "Repositories"),
            "*.cs", SearchOption.AllDirectories)
        .Where(RepositoryFiles.IsAuthored);

    /// <summary>The reader parameter a method takes and never mentions, or null.</summary>
    private static string? DroppedReader(SourceText.Member member)
    {
        var reader = ReaderParameters.FirstOrDefault(name =>
            Regex.IsMatch(member.Parameters, $@"\bGuid\??\s+{name}\b"));
        return reader is null || Regex.IsMatch(member.Body, $@"\b{reader}\b") ? null : reader;
    }

    [Fact]
    public void UseTheReaderItAsksFor()
    {
        var offenders = new List<string>();

        foreach (var file in PersistenceSources())
        {
            foreach (var member in SourceText.Members(File.ReadAllText(file)))
            {
                if (DroppedReader(member) is { } reader)
                {
                    offenders.Add($"{RepositoryFiles.Relative(file)}:{member.Line}: {member.Name} takes {reader} and never uses it");
                }
            }
        }

        offenders.Should().BeEmpty(
            "a dropped reader argument turns a scoped read into an unscoped one, and the signature " +
            "keeps promising otherwise to every call site");
    }

    [Fact]
    public void HaveASearchThatWouldFindOne()
    {
        // The rule above is a scan, and a scan that stops matching passes in silence.
        const string dropped = """
            public async Task<Post?> Get(Guid postId, Guid userId)
            {
                return await _dbContext.Posts.Where(p => p.PostId == postId).FirstOrDefaultAsync();
            }
            """;
        const string used = """
            public async Task<Post?> Get(Guid postId, Guid userId)
            {
                return await _dbContext.Rooms.Where(RoomAvailable(userId)).FirstOrDefaultAsync();
            }
            """;

        DroppedReader(SourceText.Members(dropped).Should().ContainSingle().Which)
            .Should().Be("userId", "this is the defect the rule exists for");
        DroppedReader(SourceText.Members(used).Should().ContainSingle().Which)
            .Should().BeNull("a body that mentions the reader is out of scope for this rule");
    }

    [Fact]
    public void FindTheFilesToSearch() =>
        PersistenceSources().Should().HaveCountGreaterThan(20,
            "a file walk that stops matching turns the rule above green by checking nothing");
}
