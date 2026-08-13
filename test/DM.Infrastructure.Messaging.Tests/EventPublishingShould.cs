using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Events;
using DM.Infrastructure.Messaging.GeneralBus;
using FluentAssertions;
using Jamq.Client.Abstractions.Producing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using RabbitMQ.Client.Exceptions;
using Xunit;

namespace DM.Infrastructure.Messaging.Tests;

/// <summary>
/// A broker that refuses a publish does not fail the write that produced it.
/// </summary>
/// <remarks>
/// SYSTEM.md says an event is not the carrier of the fact: publishing happens
/// outside the transaction, so a lost event costs one notification while a thrown
/// one costs correctness — the write is already committed, the caller gets a 500,
/// and a retry writes the comment twice.
///
/// The rule lives in the producer rather than at the call sites. There are around
/// seventy of them and the guard had been written at one, which is what makes this
/// worth a gate: the next call site inherits the behaviour instead of remembering
/// it.
/// </remarks>
public class EventPublishingShould
{
    [Fact]
    public async Task SwallowABrokerRefusal()
    {
        var logger = new Mock<ILogger<InvokedEventProducer>>();
        var producer = Producer(logger.Object, out var transport);

        transport
            .Setup(p => p.Send(It.IsAny<string>(), It.IsAny<InvokedEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BrokerUnreachableException(new Exception("connection refused")));

        var publish = async () => await producer.SendAsync(EventType.NewGame, Guid.NewGuid());

        await publish.Should().NotThrowAsync(
            "the write is committed by now — see the class remarks");
    }

    [Fact]
    public async Task ReportTheRefusalItSwallowed()
    {
        var logger = new Mock<ILogger<InvokedEventProducer>>();
        var producer = Producer(logger.Object, out var transport);

        transport
            .Setup(p => p.Send(It.IsAny<string>(), It.IsAny<InvokedEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BrokerUnreachableException(new Exception("connection refused")));

        await producer.SendAsync(EventType.NewGame, Guid.NewGuid());

        // Swallowing without a trace turns a dead broker into a quiet site. The
        // counter behind the alert is MessagingMetrics.PublishFailed; a log line
        // is what names the event that went missing.
        logger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task KeepPublishingAfterOneEventIsRefused()
    {
        var logger = new Mock<ILogger<InvokedEventProducer>>();
        var producer = Producer(logger.Object, out var transport);

        var attempts = 0;
        transport
            .Setup(p => p.Send(It.IsAny<string>(), It.IsAny<InvokedEvent>(), It.IsAny<CancellationToken>()))
            .Returns(() => ++attempts == 1
                ? Task.FromException(new BrokerUnreachableException(new Exception("connection refused")))
                : Task.CompletedTask);

        // The batch overload walks the list itself, so a refusal in the middle
        // must not cost the events behind it.
        await producer.SendAsync([EventType.NewGame, EventType.ChangedGame], Guid.NewGuid());

        attempts.Should().Be(2);
    }

    /// <summary>
    /// A producer whose transport is a mock.
    /// </summary>
    /// <remarks>
    /// BuildRabbit is a static extension, so the transport cannot be injected: the
    /// constructor builds a real RabbitProducer, which is harmless because it
    /// leases its channel on the first Send and never gets one. The field is then
    /// replaced with the mock this test drives.
    /// </remarks>
    private static InvokedEventProducer Producer(
        ILogger<InvokedEventProducer> logger,
        out Mock<IProducer<string, InvokedEvent>> transport)
    {
        using var provider = new ServiceCollection().AddDmJamqClient().BuildServiceProvider();
        var producer = new InvokedEventProducer(provider.GetRequiredService<IProducerBuilder>(), logger);

        transport = new Mock<IProducer<string, InvokedEvent>>();
        typeof(InvokedEventProducer)
            .GetField("producer", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(producer, transport.Object);

        return producer;
    }
}
