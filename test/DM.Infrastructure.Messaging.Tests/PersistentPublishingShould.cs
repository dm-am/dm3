using System;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using Xunit;

namespace DM.Infrastructure.Messaging.Tests;

/// <summary>
/// Nothing published may depend on the broker staying up.
/// </summary>
/// <remarks>
/// A message published with delivery mode 1 is held by the broker in memory
/// only. Queues are declared durable, which makes the queue survive a restart
/// of the broker and its contents not: dm.mail.sending was emptied along with
/// the dead-letter queue behind it. A letter is the only record that a
/// registration confirmation is owed, and the dead-letter queue is the only
/// artefact of one that could not be sent, so both went silently.
///
/// The rule used to live in a producer middleware one registration installed,
/// and the gate held that registration unique. Now it is simpler and stronger:
/// DmProducer stamps every message persistent unconditionally, there is no
/// setting to forget, and what the gate holds is that DmProducer stays the one
/// place anything publishes through.
/// </remarks>
public class PersistentPublishingShould
{
    [Fact]
    public void MarkEveryPublishedMessagePersistent()
    {
        var properties = DmMessage.DeliveryProperties();

        properties.Persistent.Should().BeTrue(
            "a non-persistent message is dropped when the broker restarts, durable " +
            "queue or not, and the mail queue is the only record that a letter is owed");
        properties.ContentType.Should().Be("application/json",
            "the codec is part of the same contract: a consumer decodes what this says it is");
    }

    /// <summary>
    /// One publishing path, or the rule above holds only for the producers that
    /// remembered it.
    /// </summary>
    /// <remarks>
    /// Two searches because there are two ways around the producer: building
    /// properties of one's own and calling the client's publish directly.
    /// Either of them compiles, runs, and loses its messages on the first
    /// broker restart — which is why this is a gate and not a review note.
    /// </remarks>
    [Fact]
    public void PublishOnlyThroughTheOneProducer()
    {
        var sources = Directory
            .GetFiles(Path.Combine(RepositoryRoot, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(path =>
                !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                    StringComparison.Ordinal) &&
                !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                    StringComparison.Ordinal))
            .ToList();

        sources.Should().NotBeEmpty("the production sources live under src/");

        var publishing = sources
            .Where(path => File.ReadAllText(path).Contains("BasicPublishAsync", StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .ToList();
        publishing.Should().Equal(["DmProducer.cs"],
            "a publish outside DmProducer bypasses the persistence and the confirm " +
            "semantics this suite is about");

        var stamping = sources
            .Where(path => File.ReadAllText(path).Contains("new BasicProperties", StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .ToList();
        stamping.Should().Equal(["DmProducer.cs"],
            "properties built anywhere else are properties nothing marks persistent");
    }

    private static string RepositoryRoot => DM.Testing.RepositoryLayout.Root;
}
