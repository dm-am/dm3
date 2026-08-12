using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Core.Enums;
using DM.Domain.Personal.Features.Notifications;
using DM.Infrastructure.Messaging.GeneralBus;
using DM.Testing;
using DM.Workers.NotificationDispatcher.Dispatching;
using DM.Workers.NotificationDispatcher.Bot;
using DM.Workers.NotificationDispatcher.Email;
using DM.Workers.NotificationDispatcher.Notifiers;
using FluentAssertions;
using Jamq.Client.Abstractions.Consuming;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace DM.Infrastructure.Messaging.Tests;

/// <summary>
/// Every delivered event runs through a retry middleware that replays the whole
/// Process on any exception, and the event carries no idempotency key, so a replay
/// cannot be told apart from a first delivery. That makes the durable write the
/// point of no return: past it a failure has to stay inside the channel that
/// produced it, otherwise the recipient gets the entry, the letter and the bot
/// message twice. Before it, throwing is the only thing that keeps an event which
/// produced nothing yet from being lost.
/// </summary>
public class NotificationProcessorShould : UnitTestBase
{
    private const EventType HandledEvent = EventType.NewTopicComment;

    private readonly Mock<INotificationService> _notificationService;
    private readonly Mock<INotificationEmailSender> _emailSender;
    private readonly Mock<INotificationBotSender> _botSender;
    private readonly Mock<IMapper> _mapper;
    private readonly RecordingRealtimeProducer _producer = new();
    private readonly InvokedEvent _event = new() { Type = HandledEvent, EntityId = Guid.NewGuid() };

    public NotificationProcessorShould()
    {
        _notificationService = Mock<INotificationService>();
        _emailSender = Mock<INotificationEmailSender>();
        _botSender = Mock<INotificationBotSender>();
        _mapper = Mock<IMapper>();

        _notificationService
            .Setup(s => s.CreateAsync(It.IsAny<IEnumerable<CreateNotification>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new CreatedNotification(
                    new CreateNotification { EventType = HandledEvent, UsersInterested = [Guid.NewGuid()] },
                    new CreateNotificationEntity
                    {
                        NotificationId = Guid.NewGuid(),
                        EventType = HandledEvent,
                        UsersInterested = [Guid.NewGuid()]
                    })
            });

        _mapper
            .Setup(m => m.Map<RealtimeNotification>(It.IsAny<object>()))
            .Returns(new RealtimeNotification());
    }

    [Fact]
    public async Task AcknowledgeTheMessageWhenAChannelFails()
    {
        _emailSender
            .Setup(s => s.SendIfEnabled(
                It.IsAny<CreateNotification>(), It.IsAny<EventType>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("the store went away"));

        var result = await Processor().Process("key", _event, CancellationToken.None);

        result.Should().Be(ProcessResult.Success,
            "the notifications are already stored, and a replay would store them a second time");
        _producer.Sent.Should().Be(1);
    }

    [Fact]
    public async Task KeepDeliveringOverTheRemainingChannelsWhenOneFails()
    {
        _producer.Fail = true;

        await Processor().Process("key", _event, CancellationToken.None);

        _emailSender.Verify(s => s.SendIfEnabled(
                It.IsAny<CreateNotification>(), It.IsAny<EventType>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _botSender.Verify(s => s.SendIfEnabled(
                It.IsAny<CreateNotification>(), It.IsAny<EventType>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task LetAFailureBeforeTheDurableWriteEscape()
    {
        _notificationService
            .Setup(s => s.CreateAsync(It.IsAny<IEnumerable<CreateNotification>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("the store went away"));

        var process = () => Processor().Process("key", _event, CancellationToken.None);

        await process.Should().ThrowAsync<InvalidOperationException>(
            "nothing was written and nothing was sent, so a replay is safe and is what makes the event survive");
    }

    private NotificationProcessor Processor() => new(
        [new StubGenerator()],
        _notificationService.Object,
        _emailSender.Object,
        _botSender.Object,
        _mapper.Object,
        _producer,
        NullLogger<NotificationProcessor>.Instance);

    /// <summary>
    /// Answers the event under test with one notification. Written out instead of
    /// mocked because the interface hands back an IAsyncEnumerable.
    /// </summary>
    private sealed class StubGenerator : INotificationGenerator
    {
        public bool CanResolve(EventType eventType) => eventType == HandledEvent;

        public async IAsyncEnumerable<CreateNotification> Generate(Guid entityId)
        {
            await Task.CompletedTask;
            yield return new CreateNotification
            {
                EventType = HandledEvent,
                UsersInterested = [Guid.NewGuid()],
                Metadata = "{}"
            };
        }
    }

    /// <summary>
    /// Counts realtime pushes and can fail the way a broker that went away does.
    /// </summary>
    private sealed class RecordingRealtimeProducer : IRealtimeNotificationProducer
    {
        public bool Fail { get; set; }

        public int Sent { get; private set; }

        public Task SendAsync(RealtimeNotification notification, CancellationToken cancellationToken)
        {
            if (Fail)
            {
                throw new InvalidOperationException("the broker went away");
            }

            Sent++;
            return Task.CompletedTask;
        }
    }
}
