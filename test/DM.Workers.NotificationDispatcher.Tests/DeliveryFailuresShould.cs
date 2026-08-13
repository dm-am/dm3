using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Core.Enums;
using DM.Domain.Personal.Features.Notifications;
using DM.Infrastructure.Messaging.GeneralBus;
using DM.Workers.NotificationDispatcher.Bot;
using DM.Workers.NotificationDispatcher.Dispatching;
using DM.Workers.NotificationDispatcher.Email;
using DM.Workers.NotificationDispatcher.Notifiers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DM.Workers.NotificationDispatcher.Tests;

/// <summary>
/// A notification that reached no recipient is counted.
/// </summary>
/// <remarks>
/// Delivery is best effort by design: the notification is already durable when
/// the senders run, the event carries no idempotency key, and letting one dead
/// channel throw would replay the whole message and cost the recipient a second
/// copy over every channel that is up. So a failure is swallowed - and swallowed
/// is where it stopped. The message was consumed successfully, because from the
/// pipeline's side it was, so every counter about the queue and every panel about
/// the process stayed exactly as green as on a day when everything arrived.
///
/// The reason this needs a test rather than a reading of the code: the counter is
/// in a catch block that nothing else observes. Delete the line and every test in
/// this repository still passes, the worker still runs, the log still carries its
/// warning, and the only difference is that the rule reading the series never
/// fires again.
/// </remarks>
public class DeliveryFailuresShould
{
    /// <summary>The channel whose sender is made to fail, and what it is counted as.</summary>
    [Theory]
    [InlineData("realtime")]
    [InlineData("email")]
    [InlineData("bot")]
    public async Task CountTheNotificationTheChannelNeverTook(string channel)
    {
        var counted = new List<string?>();
        using var listener = Listening(counted);

        await Process(failing: channel);

        counted.Should().Contain(channel,
            $"the {channel} channel refused the notification, the pipeline reported success " +
            "because from its side there was nothing wrong, and this counter is the only " +
            "thing left that says a reader never got it");
    }

    [Fact]
    public async Task CountNothingWhenEveryChannelTakesIt()
    {
        var counted = new List<string?>();
        using var listener = Listening(counted);

        await Process(failing: null);

        counted.Should().BeEmpty(
            "a counter that also rises on success is a counter no rule can be written from");
    }

    /// <summary>Runs one event through the processor with one channel refusing.</summary>
    private static async Task Process(string? failing)
    {
        var notification = new CreateNotification
        {
            UsersInterested = [Guid.NewGuid()],
            EventType = EventType.NewTopic,
            Metadata = new object(),
        };

        var generator = new Mock<INotificationGenerator>();
        generator.Setup(g => g.CanResolve(It.IsAny<EventType>())).Returns(true);
        generator.Setup(g => g.Generate(It.IsAny<Guid>())).Returns(One(notification));

        var service = new Mock<INotificationService>();
        service
            .Setup(s => s.CreateAsync(It.IsAny<IReadOnlyList<CreateNotification>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new CreatedNotification(notification, new CreateNotificationEntity
                {
                    NotificationId = Guid.NewGuid(),
                    EventType = notification.EventType,
                    UsersInterested = notification.UsersInterested,
                }),
            });

        var producer = new Mock<IRealtimeNotificationProducer>();
        var email = new Mock<INotificationEmailSender>();
        var bot = new Mock<INotificationBotSender>();

        var refused = new InvalidOperationException("the channel refused it");
        if (failing == "realtime")
        {
            producer
                .Setup(p => p.SendAsync(It.IsAny<RealtimeNotification>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(refused);
        }

        if (failing == "email")
        {
            email
                .Setup(s => s.SendIfEnabled(It.IsAny<CreateNotification>(), It.IsAny<EventType>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(refused);
        }

        if (failing == "bot")
        {
            bot
                .Setup(s => s.SendIfEnabled(It.IsAny<CreateNotification>(), It.IsAny<EventType>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(refused);
        }

        var mapper = new Mock<IMapper>();
        mapper
            .Setup(m => m.Map<RealtimeNotification>(It.IsAny<CreateNotificationEntity>()))
            .Returns(new RealtimeNotification());

        var processor = new NotificationProcessor(
            [generator.Object],
            service.Object,
            email.Object,
            bot.Object,
            mapper.Object,
            producer.Object,
            Mock.Of<ILogger<NotificationProcessor>>());

        var result = await processor.Process("key",
            new InvokedEvent { Type = EventType.NewTopic, EntityId = Guid.NewGuid() },
            CancellationToken.None);

        result.Should().Be(Jamq.Client.Abstractions.Consuming.ProcessResult.Success,
            "a failed delivery must not hand the message back to the retry ladder: the " +
            "notification is already written, and a replay writes it again");
    }

    /// <summary>Channels named by measurements of the delivery counter.</summary>
    private static MeterListener Listening(ICollection<string?> counted)
    {
        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, self) =>
            {
                if (instrument.Name == "dm.messaging.delivery_failed")
                {
                    self.EnableMeasurementEvents(instrument);
                }
            },
        };

        listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
        {
            foreach (var tag in tags)
            {
                if (tag.Key == "channel")
                {
                    counted.Add(tag.Value?.ToString());
                }
            }
        });

        listener.Start();
        return listener;
    }

    private static async IAsyncEnumerable<CreateNotification> One(CreateNotification notification)
    {
        yield return notification;
        await Task.CompletedTask;
    }
}
