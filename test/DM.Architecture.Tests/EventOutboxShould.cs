using System;
using System.IO;
using System.Linq;
using DM.Domain.Core.Events;
using AwesomeAssertions;
using DM.Testing;
using Xunit;

namespace DM.Architecture.Tests;

/// <remarks>
/// Publishing an event is an INSERT into OutboxEvents, which the notification worker claims from.
/// Asserted on the sources for a call, on the assemblies for a count.
/// </remarks>
public class EventOutboxShould
{
    private const string Producer = "src/DM.Infrastructure.Persistence/RelationalStorage/OutboxEventProducer.cs";

    /// <summary>
    /// Exactly one implementation of IEventProducer in the production
    /// assemblies: a second one would return silent fire-and-forget to
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
            "an event that reaches a worker without passing through the outbox is an " +
            "event a process restart loses silently, which is the loss this table exists " +
            "to close");
    }

    /// <summary>
    /// The producer neither opens a transaction nor wraps itself in an execution strategy: the insert
    /// is a separate commit after the domain one.
    /// </summary>
    [Fact]
    public void KeepTheProducerOutOfTransactions()
    {
        var text = File.ReadAllText(Path.Combine(RepositoryLayout.Root, Producer));

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
}
