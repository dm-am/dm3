using System;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using DM.Testing;
using Xunit;

namespace DM.Architecture.Tests;

/// <remarks>
/// A queue with no dead-letter exchange has its poison messages dropped by the broker. Asserted
/// against the sources: the parameters are built behind a live broker.
/// </remarks>
public class DeadLetterRoutingShould
{
    private const string ConsumerParameters = "new DmConsumerParameters(";

    private const string RoutesPoisonMessages = "DeadLetterExchange =";

    // Our own declaration of that exchange, which the client's has to match.
    private const string DeclaresTheDestination = "DeadLetterQueue.DeclareTerminal(";

    /// <summary>
    /// The realtime push queue is exempt: its message copies a stored notification the client reads over REST.
    /// </summary>
    private const string ExemptConsumer = "RealtimeNotificationConsumer.cs";


    private static string[] Consumers => RepositoryFiles.ProductionSources()
        .Where(path => File.ReadAllText(path).Contains(ConsumerParameters, StringComparison.Ordinal))
        .ToArray();

    // A search that finds no consumer leaves the rules below green over nothing.
    [Fact]
    public void FindEveryConsumer() =>
        Consumers.Should().NotBeEmpty();

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

    private const string SharedConsumer = "src/DM.Infrastructure.Messaging/DmConsumer.cs";

    /// <remarks>
    /// Without a prefetch limit the worker holds the whole queue unacknowledged, and the ready series
    /// shows it empty. global=false is spelled out: RabbitMQ 4.x refuses global=true.
    /// </remarks>
    [Fact]
    public void BoundWhatEachConsumerTakesAtOnce() =>
        File.ReadAllText(Path.Combine(RepositoryLayout.Root,
                SharedConsumer.Replace('/', Path.DirectorySeparatorChar)))
            .Should().Contain("BasicQosAsync(0, 1, global: false",
                "every subscription goes through this file, and without the prefetch " +
                "of one the worker holds the whole queue in memory as a backlog no " +
                "depth series can see");
}
