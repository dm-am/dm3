using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// Nothing is announced before the state it announces is durable.
/// </summary>
/// <remarks>
/// An event on the bus is read by consumers that send letters and bot messages,
/// and neither can be taken back. Publishing while the write is still in the
/// change tracker makes the announcement the reliable half and the record the
/// unreliable one: the reminder loop marked every stale pendency in memory,
/// published a reminder for each, and saved the batch afterwards, so a lost
/// connection to Postgres past that point sent the masters letters the database
/// holds no trace of - and twelve hours later sent them again, until one save
/// finally landed. Stopping the process between the loop and the save does the
/// same thing.
///
/// Asserted on the text, because the order of two awaits inside one method is
/// not something the type system or the IL has an opinion about, and the callers
/// need a database and a broker to run at all.
///
/// Two rules, because the code has two shapes. Where one method both writes and
/// announces, the order of the two calls is what there is to check. Where the
/// write moved behind a repository - which is where the background jobs put it
/// when they stopped holding the context - the announcement is made out of what
/// the committing call returned, so the second rule checks that the call comes
/// first and that the repository behind it commits before it answers.
/// </remarks>
public class PublishAfterCommitShould
{
    /// <summary>Commit of everything the change tracker holds.</summary>
    private const string Commit = "SaveChangesAsync(";

    /// <summary>
    /// Every way a repository here makes a write durable. The change tracker is
    /// one of them; the two set-based operations write and commit in one statement
    /// and never touch it, which is how the inactivity sweep writes.
    /// </summary>
    private static readonly string[] DurableWrite =
        [Commit, "ExecuteUpdateAsync(", "ExecuteDeleteAsync("];

    /// <summary>Publication of a domain event, as every call site spells it.</summary>
    private const string Publish = "SendAsync(EventType.";

    /// <summary>
    /// The pairs that split the write from the announcement: the processor that
    /// publishes, and the repository call it has to get its answer from first.
    /// </summary>
    /// <remarks>
    /// A list, because "which call is the claim" is not derivable from the text.
    /// It is held to the tree by the rule below rather than kept by hand: every
    /// processor that publishes and does not commit is a processor of this shape,
    /// and the third one written the same way used to be covered by nothing.
    /// </remarks>
    private static readonly (string Processor, string CommittingCall, string Repository)[] ClaimThenAnnounce =
    [
        ("src/DM.Domain.Game/Features/PostPendencies/PendencyReminderProcessor.cs",
            "ClaimPendingReminders(",
            "src/DM.Infrastructure.Persistence/Repositories/Game/PostPendencyRepository.cs"),
        ("src/DM.Domain.Forum/Features/Digests/PeriodDigestProcessor.cs",
            "TryRecordDigest(",
            "src/DM.Infrastructure.Persistence/Repositories/Forum/PeriodDigestRepository.cs"),
        ("src/DM.Domain.Game/Features/Inactivity/GameInactivityProcessor.cs",
            "SetInactivityWarning(",
            "src/DM.Infrastructure.Persistence/Repositories/Game/InactivityRepository.cs"),
    ];

    private static string[] Sources => Directory
        .EnumerateFiles(Path.Combine(RepositoryRoot, "src"), "*.cs", SearchOption.AllDirectories)
        .Where(IsAuthored)
        .ToArray();

    private static string[] ServicesThatDoBoth => Sources
        .Where(path =>
        {
            var text = File.ReadAllText(path);
            return text.Contains(Commit, StringComparison.Ordinal) &&
                   text.Contains(Publish, StringComparison.Ordinal);
        })
        .ToArray();

    /// <summary>
    /// A rule that matches nothing passes, and both halves of this one match on
    /// a spelling. Checked separately rather than through their overlap: the
    /// overlap is empty today and being empty is the point of the third rule, so
    /// an assertion on it would fail for the good outcome.
    /// </summary>
    [Fact]
    public void FindBothCallsInTheTree()
    {
        Sources.Count(path => File.ReadAllText(path).Contains(Commit, StringComparison.Ordinal))
            .Should().BeGreaterThan(20, "the commit is spelled this way all over the persistence layer");

        Sources.Count(path => File.ReadAllText(path).Contains(Publish, StringComparison.Ordinal))
            .Should().BeGreaterThan(5, "every publication of a domain event is spelled this way");
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
        var root = RepositoryRoot;

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

            // The method body and not the rest of the file: "a commit somewhere
            // below" is satisfied by the next method down, which is not the one
            // the caller is announcing out of.
            // Compensation is not the claim. The digest claim writes the marker in
            // the try and takes the topic back in the catch, both durably, so a
            // body read whole stays green with the claim itself deleted.
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
    /// <remarks>
    /// The first rule reads the files that both write and announce, and that set is
    /// empty today: the background jobs put their writes behind repositories, which
    /// is the whole point of the second rule. An empty set is a rule that passes
    /// over nothing, so the coverage is asserted from the other end - a processor
    /// that publishes either commits in the same file (rule one) or is named in the
    /// pair list (rule two), and a third one written like the other two cannot slip
    /// between them.
    /// </remarks>
    [Fact]
    public void CoverEveryProcessorThatAnnounces()
    {
        var announcing = Sources
            .Where(path => Path.GetFileName(path).EndsWith("Processor.cs", StringComparison.Ordinal))
            .Where(path => File.ReadAllText(path).Contains(Publish, StringComparison.Ordinal))
            .ToArray();

        announcing.Should().HaveCountGreaterThan(2,
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

    private static bool IsAuthored(string path) => !path
        .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
        .Any(segment => segment is "obj" or "bin" or "node_modules");

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
}
