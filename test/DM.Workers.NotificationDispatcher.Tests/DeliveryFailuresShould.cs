using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Personal.Features.Notifications;
using DM.Infrastructure.Messaging.GeneralBus;
using DM.Workers.NotificationDispatcher.Bot;
using DM.Workers.NotificationDispatcher.Dispatching;
using DM.Workers.NotificationDispatcher.Email;
using DM.Workers.NotificationDispatcher.Notifiers;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace DM.Workers.NotificationDispatcher.Tests;

/// <summary>
/// A notification that reached no recipient is counted.
/// </summary>
/// <remarks>
/// Delivery is best effort and a failure is swallowed, so the message is consumed successfully; the counter
/// in the catch block is the only trace, and nothing else observes it.
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

        var generator = Substitute.For<INotificationGenerator>();
        generator.CanResolve(Arg.Any<EventType>()).Returns(true);
        generator.Generate(Arg.Any<Guid>()).Returns(NotificationSeed.One(notification));

        var service = Substitute.For<INotificationService>();
        service
            .CreateAsync(Arg.Any<IReadOnlyList<CreateNotification>>(), Arg.Any<CancellationToken>()).Returns(new[]
            {
                new CreatedNotification(notification, new CreateNotificationEntity
                {
                    NotificationId = Guid.NewGuid(),
                    EventType = notification.EventType,
                    UsersInterested = notification.UsersInterested,
                }),
            });

        var producer = Substitute.For<IRealtimeNotificationProducer>();
        var email = Substitute.For<INotificationEmailSender>();
        var bot = Substitute.For<INotificationBotSender>();

        var refused = new InvalidOperationException("the channel refused it");
        if (failing == "realtime")
        {
            producer
                .SendAsync(Arg.Any<RealtimeNotification>(), Arg.Any<CancellationToken>()).ThrowsAsync(refused);
        }

        if (failing == "email")
        {
            email
                .SendIfEnabled(Arg.Any<CreateNotification>(), Arg.Any<EventType>(),
                    Arg.Any<CancellationToken>()).ThrowsAsync(refused);
        }

        if (failing == "bot")
        {
            bot
                .SendIfEnabled(Arg.Any<CreateNotification>(), Arg.Any<EventType>(),
                    Arg.Any<CancellationToken>()).ThrowsAsync(refused);
        }

        var processor = new NotificationProcessor(
            [generator],
            service,
            email,
            bot,
            producer,
            Substitute.For<ILogger<NotificationProcessor>>());

        var result = await processor.Process("key",
            new InvokedEvent { Type = EventType.NewTopic, EntityId = Guid.NewGuid() },
            CancellationToken.None);

        result.Should().Be(DM.Infrastructure.Messaging.ProcessResult.Success,
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

}
