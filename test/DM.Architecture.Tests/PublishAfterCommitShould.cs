using System;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using DM.Testing;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// Nothing is announced before the state it announces is durable.
/// </summary>
/// <remarks>
/// Consumers send letters that cannot be taken back, and SendAsync saves the scoped context it shares
/// with the caller, so it comes after the domain commit. Asserted on the text: the order of two awaits.
/// One rule where a method writes and announces, one where the write is behind a repository.
/// </remarks>
public class PublishAfterCommitShould
{
    /// <summary>Commit of everything the change tracker holds.</summary>
    private const string Commit = "SaveChangesAsync(";

    /// <summary>
    /// Every way a repository here makes a write durable: the change tracker, the set-based operations and
    /// raw SQL, as in DurableWritesShould.
    /// </summary>
    private static readonly string[] DurableWrite =
    [
        Commit,
        "ExecuteUpdateAsync(",
        "ExecuteDeleteAsync(",
        "ExecuteSqlRawAsync(",
        "ExecuteSqlInterpolatedAsync(",
    ];

    /// <summary>Publication of a domain event, as every call site spells it.</summary>
    private const string Publish = "SendAsync(EventType.";

    /// <summary>
    /// The pairs that split the write from the announcement: the processor that
    /// publishes, and the repository call it has to get its answer from first.
    /// </summary>
    /// <remarks>A list: which call is the claim cannot be derived from the text. The rule below holds it to the tree.</remarks>
    private static readonly (string Processor, string CommittingCall, string Repository)[] ClaimThenAnnounce =
    [
        ("src/DM.Domain.Game/Features/PostPendencies/PendencyReminderProcessor.cs",
            "ClaimPendingReminders(",
            "src/DM.Infrastructure.Persistence/Repositories/Game/PostPendencyRepository.cs"),
        ("src/DM.Domain.Forum/Features/Digests/PeriodDigestProcessor.cs",
            "TryClaimPeriod(",
            "src/DM.Infrastructure.Persistence/Repositories/Forum/PeriodDigestRepository.cs"),
        ("src/DM.Domain.Game/Features/Inactivity/GameInactivityProcessor.cs",
            "SetInactivityWarning(",
            "src/DM.Infrastructure.Persistence/Repositories/Game/InactivityRepository.cs"),
    ];

    private static readonly string[] Sources = RepositoryFiles.ProductionSources()
        .ToArray();

    private static readonly string[] ServicesThatDoBoth = Sources
        .Where(path =>
        {
            var text = File.ReadAllText(path);
            return text.Contains(Commit, StringComparison.Ordinal) &&
                   text.Contains(Publish, StringComparison.Ordinal);
        })
        .ToArray();

    /// <summary>
    /// A rule that matches nothing passes; both halves are checked apart, their overlap is empty by design.
    /// </summary>
    [Fact]
    public void FindBothCallsInTheTree()
    {
        Sources.Count(path => File.ReadAllText(path).Contains(Commit, StringComparison.Ordinal))
            .Should().BePositive("the commit is spelled this way all over the persistence layer");

        Sources.Count(path => File.ReadAllText(path).Contains(Publish, StringComparison.Ordinal))
            .Should().BePositive("every publication of a domain event is spelled this way");
    }

    [Fact]
    public void CommitBeforeTheFirstPublication() =>
        ServicesThatDoBoth
            .Where(path =>
            {
                var text = File.ReadAllText(path);
                return text.IndexOf(Publish, StringComparison.Ordinal) <
                       text.LastIndexOf(Commit, StringComparison.Ordinal);
            })
            .Select(Path.GetFileName)
            .Should().BeEmpty(
                "an event published while its own write is still pending is an " +
                "announcement that cannot be taken back about a state that may never " +
                "arrive, and the pass that runs next makes the same announcement again");

    [Fact]
    public void ClaimBeforeAnnouncingWhereTheWriteMovedBehindARepository()
    {
        var root = RepositoryLayout.Root;

        foreach (var (processor, committingCall, repository) in ClaimThenAnnounce)
        {
            var processorText = File.ReadAllText(Path.Combine(root, processor));
            var claimAt = processorText.IndexOf(committingCall, StringComparison.Ordinal);
            var publishAt = processorText.IndexOf(Publish, StringComparison.Ordinal);

            claimAt.Should().BeGreaterThan(-1, $"{processor} must still call {committingCall}");
            publishAt.Should().BeGreaterThan(-1, $"{processor} must still publish an event");
            claimAt.Should().BeLessThan(publishAt,
                $"{processor} announces what {committingCall} handed back, so a publication " +
                "before it would be an announcement about a state nothing has written yet");

            var repositoryText = File.ReadAllText(Path.Combine(root, repository));
            var callAt = repositoryText.IndexOf(committingCall, StringComparison.Ordinal);
            callAt.Should().BeGreaterThan(-1, $"{repository} must still implement {committingCall}");

            // The method body only, catch blocks left out: compensation is not the claim.
            var body = WithoutCatchBlocks(BodyAt(repositoryText, callAt));
            DurableWrite.Any(write => body.Contains(write, StringComparison.Ordinal))
                .Should().BeTrue(
                    $"{repository} answers {committingCall} with what the caller will announce, so " +
                    "the marker that stops the announcement repeating has to be written durably " +
                    "inside it");
        }
    }

    /// <summary>
    /// Every processor that announces is under one of the two rules above.
    /// </summary>
    /// <remarks>The first rule's set is empty today, so coverage is asserted from the other end.</remarks>
    [Fact]
    public void CoverEveryProcessorThatAnnounces()
    {
        var announcing = Sources
            .Where(path => Path.GetFileName(path).EndsWith("Processor.cs", StringComparison.Ordinal))
            .Where(path => File.ReadAllText(path).Contains(Publish, StringComparison.Ordinal))
            .ToArray();

        announcing.Should().NotBeEmpty(
            "the background processors are what this file is about, and finding none " +
            "would report green over an empty tree");

        var named = ClaimThenAnnounce
            .Select(pair => Path.GetFileName(pair.Processor))
            .ToHashSet(StringComparer.Ordinal);

        announcing
            .Where(path => !File.ReadAllText(path).Contains(Commit, StringComparison.Ordinal))
            .Select(path => Path.GetFileName(path)!)
            .Where(name => !named.Contains(name))
            .Should().BeEmpty(
                "a processor that publishes without committing announces what a repository " +
                "call handed it, and the order of those two is what the pair list checks");
    }

    /// <summary>The same text with every catch block cut out of it.</summary>
    private static string WithoutCatchBlocks(string body)
    {
        while (true)
        {
            var at = body.IndexOf("catch", StringComparison.Ordinal);
            if (at < 0)
            {
                return body;
            }

            var block = BodyAt(body, at);
            if (block.Length == 0)
            {
                return body[..at];
            }

            var start = body.IndexOf(block, at, StringComparison.Ordinal);
            body = body[..at] + body[(start + block.Length)..];
        }
    }

    /// <summary>The body of the method whose signature holds <paramref name="at" />.</summary>
    private static string BodyAt(string text, int at)
    {
        var open = text.IndexOf('{', at);
        if (open < 0)
        {
            return string.Empty;
        }

        var depth = 0;
        for (var i = open; i < text.Length; i++)
        {
            if (text[i] == '{')
            {
                depth++;
            }
            else if (text[i] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return text[open..(i + 1)];
                }
            }
        }

        return text[open..];
    }
}
