using System;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// A message no consumer could process has to stop somewhere a human can reach.
/// </summary>
/// <remarks>
/// When the retry middleware runs out of attempts the exception escapes the
/// pipeline and the message is rejected without requeue. Whether that means kept
/// or gone is decided by one line of the consumer parameters: a queue with no
/// dead-letter exchange has its poison messages dropped by the broker. The mail
/// worker carried that line and the notification dispatcher did not, so one
/// generator throwing on its own data cost the recipient the notification, and
/// the only trace left of the event was five warnings from the middleware.
///
/// Asserted against the sources: the parameters are built inside a background
/// service that needs a live broker to get that far, so nothing but the text of
/// the consumers says which queues carry work that has to outlive a failure.
/// </remarks>
public class DeadLetterRoutingShould
{
    /// <summary>How every Rabbit consumer in the solution declares its topology.</summary>
    private const string ConsumerParameters = "new DmConsumerParameters(";

    /// <summary>Assignment of the dead-letter exchange inside those parameters.</summary>
    private const string RoutesPoisonMessages = "DeadLetterExchange =";

    /// <summary>Our own declaration of that exchange, which the client's has to match.</summary>
    private const string DeclaresTheDestination = "DeadLetterQueue.DeclareTerminal(";

    /// <summary>
    /// The realtime push queue, exempt by decision rather than by omission: its
    /// message is a copy of a notification the dispatcher has already stored, and
    /// the client reads that over REST. A push kept past its moment gives nobody
    /// anything to act on.
    /// </summary>
    private const string ExemptConsumer = "RealtimeNotificationConsumer.cs";

    private static string RepositoryRoot => DM.Testing.RepositoryLayout.Root;

    private static string[] Consumers => Directory
        .EnumerateFiles(Path.Combine(RepositoryRoot, "src"), "*.cs", SearchOption.AllDirectories)
        .Where(IsAuthored)
        .Where(path => File.ReadAllText(path).Contains(ConsumerParameters, StringComparison.Ordinal))
        .ToArray();

    /// <summary>
    /// A rule that matches nothing passes. Three consumers exist — mail,
    /// notifications and the realtime push — so finding fewer means the search
    /// string went stale rather than that the tree is clean.
    /// </summary>
    [Fact]
    public void FindEveryConsumer() =>
        Consumers.Should().HaveCountGreaterThanOrEqualTo(3);

    [Fact]
    public void DeadLetterEveryQueueThatCarriesWork() =>
        Consumers
            .Where(path => Path.GetFileName(path) != ExemptConsumer)
            .Where(path => !File.ReadAllText(path).Contains(RoutesPoisonMessages, StringComparison.Ordinal))
            .Select(path => Path.GetFileName(path))
            .Should().BeEmpty(
                "a queue without a dead-letter exchange has the broker discard whatever its " +
                "consumer could not process, and a discarded event leaves nothing to restore " +
                "the notification, the letter, or even the fact that either was due");

    [Fact]
    public void DeclareWhateverIsDeadLetteredInto() =>
        Consumers
            .Where(path => File.ReadAllText(path).Contains(RoutesPoisonMessages, StringComparison.Ordinal))
            .Where(path => !File.ReadAllText(path).Contains(DeclaresTheDestination, StringComparison.Ordinal))
            .Select(path => Path.GetFileName(path))
            .Should().BeEmpty(
                "the client declares this topology as well, and the two have to agree " +
                "argument for argument: a declaration missing here leaves the destination to " +
                "an internal of a pinned version, and one that drifts is answered with 406 " +
                "when the consumer subscribes");

    /// <summary>The one file every subscription of this system goes through.</summary>
    private const string SharedConsumer = "src/DM.Infrastructure.Messaging/DmConsumer.cs";

    /// <summary>
    /// A consumer takes what it can finish, not what the queue holds.
    /// </summary>
    /// <remarks>
    /// Without a prefetch limit the broker hands over the entire queue and the
    /// worker holds every message of it in memory, unacknowledged, until it
    /// works through them. Nothing is gained for that: the handler runs on the
    /// client's dispatcher, one message at a time on the channel either way.
    ///
    /// And the backlog it builds is the invisible kind. A message delivered to a
    /// consumer is no longer ready, so depth read off the ready series shows an
    /// empty queue for a worker sitting on a thousand messages — the alert stays
    /// green, the panel stays flat, and the first symptom is a reader asking why
    /// a notification took an hour.
    ///
    /// The limit lives in the one shared subscription rather than in each
    /// consumer's parameters, so this asserts the line itself: prefetch of one,
    /// and global=false spelled out, because global=true is the call RabbitMQ
    /// 4.x refuses.
    /// </remarks>
    [Fact]
    public void BoundWhatEachConsumerTakesAtOnce() =>
        File.ReadAllText(Path.Combine(RepositoryRoot,
                SharedConsumer.Replace('/', Path.DirectorySeparatorChar)))
            .Should().Contain("BasicQosAsync(0, 1, global: false",
                "every subscription goes through this file, and without the prefetch " +
                "of one the worker holds the whole queue in memory as a backlog no " +
                "depth series can see");

    /// <summary>
    /// The exemption has to keep naming a consumer that exists, or a rename turns
    /// it into a hole nobody sees.
    /// </summary>
    [Fact]
    public void KeepTheExemptionNamingAConsumer() =>
        Consumers.Select(path => Path.GetFileName(path)).Should().Contain(ExemptConsumer);

    private static bool IsAuthored(string path) =>
        !path
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment => segment is "obj" or "bin" or "node_modules");
}
