using System;
using System.IO;
using System.Linq;
using DM.Domain.Core.Events;
using AwesomeAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// The properties of the event outbox that no test of its behavior can hold.
/// </summary>
/// <remarks>
/// From W1.5 on, publishing a domain event is an INSERT into OutboxEvents and
/// the relay is the one thing that talks to the exchange. Each rule here guards
/// a way that arrangement can quietly stop being true: a second IEventProducer
/// implementation would bring fire-and-forget back without touching a call
/// site; a transaction inside the producer would either swallow the caller's
/// changes or promise an atomicity the design explicitly does not have; a claim
/// without SKIP LOCKED would let the two relays of a deployment window mark
/// each other's rows; and a mark written before the publish would record
/// "delivered" about a message the broker never took.
///
/// Asserted on the sources where the property is an order of calls or a SQL
/// clause, and on the loaded assemblies where it is a count of types - the same
/// split every neighbor of this file uses.
/// </remarks>
public class EventOutboxShould
{
    private const string Producer = "src/DM.Infrastructure.Persistence/RelationalStorage/OutboxEventProducer.cs";
    private const string Processor = "src/DM.Infrastructure.Persistence/RelationalStorage/OutboxRelayProcessor.cs";

    /// <summary>
    /// Exactly one implementation of IEventProducer in the production
    /// assemblies (INV-7): a second one would return silent fire-and-forget to
    /// whichever host resolved it last.
    /// </summary>
    [Fact]
    public void KeepOneEventProducerImplementation()
    {
        var implementations = ProductionAssemblies.Types()
            .Where(type => type is { IsAbstract: false, IsClass: true })
            .Where(type => typeof(IEventProducer).IsAssignableFrom(type))
            .Select(type => type.Name)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        implementations.Should().Equal(["OutboxEventProducer"],
            "an event that reaches the broker without passing through the outbox is an " +
            "event a broker restart loses silently, which is the loss this table exists " +
            "to close");
    }

    /// <summary>
    /// The producer neither opens a transaction nor wraps itself in an execution
    /// strategy (INV-4): the insert is a second, separate commit after the
    /// domain one - joining a transaction would smuggle in an atomicity the
    /// design rejected, and opening one would wrap a single write for nothing.
    /// </summary>
    [Fact]
    public void KeepTheProducerOutOfTransactions()
    {
        var text = File.ReadAllText(Path.Combine(RepositoryRoot, Producer));

        text.Should().Contain("SaveChangesAsync(",
            "the producer stores the event with one commit of the scoped context, and a " +
            "file without the call is not the producer this rule is about");
        text.Should().NotContain("BeginTransactionAsync",
            "the insert is deliberately its own commit after the domain one - the W1.1 " +
            "compromise - and a transaction here would either be pointless or grow into " +
            "joining the caller's");
        text.Should().NotContain("CreateExecutionStrategy",
            "a replayed strategy delegate re-runs everything inside it, and the producer " +
            "has exactly one write with nothing to replay");
    }

    /// <summary>
    /// The claim locks what it takes and skips what is locked (D9), and the
    /// mark follows the publish (INV-2).
    /// </summary>
    [Fact]
    public void ClaimWithSkipLockedAndMarkOnlyAfterPublishing()
    {
        var text = File.ReadAllText(Path.Combine(RepositoryRoot, Processor));

        text.Should().Contain("FOR UPDATE SKIP LOCKED",
            "in the deployment window two relays run at once, and without the lock both " +
            "would publish and - worse - both would mark the same rows");

        var publishAt = text.IndexOf("await publish(", StringComparison.Ordinal);
        var markAt = text.IndexOf(".PublishedUtc = ", StringComparison.Ordinal);

        publishAt.Should().BeGreaterThan(-1, "the processor must run each row through the delegate");
        markAt.Should().BeGreaterThan(-1, "the processor must mark the rows it published");
        publishAt.Should().BeLessThan(markAt,
            "a row marked before its publish returned is a row recorded as delivered on " +
            "a confirm that never arrived - the silent loss coming back through another door");
    }

    /// <summary>
    /// Only the relay builds a producer for the events exchange (INV-7): the
    /// relay is what ties the publish to the confirm and the confirm to the
    /// mark, and a second channel to dm.events would bypass all three.
    /// </summary>
    [Fact]
    public void LetOnlyTheRelayBuildTheEventsProducer()
    {
        var building = RepositoryFiles.ProductionSources()
            .Where(path => File.ReadAllText(path).Contains("Build<InvokedEvent>", StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .ToList();

        building.Should().Equal(["OutboxRelayService.cs"],
            "whoever publishes InvokedEvent outside the relay publishes rows nothing " +
            "stored, with EventIds nothing can retry, into an ordering nothing enforces");
    }

    private static string RepositoryRoot => DM.Testing.RepositoryLayout.Root;
}
