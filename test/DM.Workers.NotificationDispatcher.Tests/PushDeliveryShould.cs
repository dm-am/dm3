using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using AwesomeAssertions;
using DM.Domain.Core.Enums;
using DM.Domain.Personal.Features.Notifications;
using DM.Infrastructure.Core.Queues;
using DM.Infrastructure.Messaging.GeneralBus;
using DM.Workers.NotificationDispatcher.Bot;
using DM.Workers.NotificationDispatcher.Dispatching;
using DM.Workers.NotificationDispatcher.Email;
using DM.Workers.NotificationDispatcher.Notifiers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using NSubstitute;
using Xunit;

namespace DM.Workers.NotificationDispatcher.Tests;

/// <summary>
/// A push leaves the notification worker as database notifications on the push channel,
/// heard here by a test listener the way the listener of an API instance hears them.
/// </summary>
/// <remarks>
/// Mutation every recipient test must fail on: sending the push without cutting it, which
/// the database refuses past 8000 bytes.
/// </remarks>
[Collection(NotificationDatabaseCollection.Name)]
public class PushDeliveryShould(NotificationDatabaseFixture fixture)
{
    private static readonly TimeSpan HeardWithin = TimeSpan.FromSeconds(5);

    /// <remarks>Mutation it must fail on: a processor that stores the notification and sends no push.</remarks>
    [Fact]
    public async Task GiveAPushHeardByAListenerForAProcessedEvent()
    {
        var notificationId = Guid.NewGuid();
        await using var listener = await TestListener.Start(fixture.ConnectionString);
        await using var dataSource = NpgsqlDataSource.Create(fixture.ConnectionString);

        await Processor(dataSource, notificationId).Process(QueueNames.Notifications,
            new InvokedEvent { Type = EventType.NewTopic, EntityId = Guid.NewGuid(), EventId = Guid.NewGuid() },
            CancellationToken.None);

        var heard = await listener.HearPushes(notificationId, expectedRecipients: 1);
        heard.Single().RecipientIds.Should().Equal(NotificationSeed.MasterId);
    }

    [Fact]
    public async Task ReachEachOfAThousandRecipientsExactlyOnce()
    {
        var push = Push(Enumerable.Range(0, 1000).Select(_ => Guid.NewGuid()).ToArray());
        await using var listener = await TestListener.Start(fixture.ConnectionString);
        await using var dataSource = NpgsqlDataSource.Create(fixture.ConnectionString);

        await new RealtimeNotificationProducer(dataSource).SendAsync(push, CancellationToken.None);

        var heard = await listener.HearPushes(push.NotificationId, expectedRecipients: 1000);
        heard.SelectMany(piece => piece.RecipientIds).Should().BeEquivalentTo(push.RecipientIds,
            "every recipient is in exactly one notification");
        listener.Payloads.Should().AllSatisfy(payload =>
            Encoding.UTF8.GetByteCount(payload).Should().BeLessThan(7000));
    }

    // Mutation: a sender that joins the caller's ambient transaction, whose rollback swallows the push.
    [Fact]
    public async Task GoOutsideTheCallersTransactionAndStayUnstored()
    {
        var push = Push([Guid.NewGuid()]);
        await using var listener = await TestListener.Start(fixture.ConnectionString);
        await using var dataSource = NpgsqlDataSource.Create(fixture.ConnectionString);
        var outboxRowsBefore = await OutboxRows();

        using (new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
        {
            await new RealtimeNotificationProducer(dataSource).SendAsync(push, CancellationToken.None);

            // Not completed: the caller's work rolls back, the push has already gone.
        }

        await listener.HearPushes(push.NotificationId, expectedRecipients: 1);
        (await OutboxRows()).Should().Be(outboxRowsBefore, "a push is never stored");
    }

    private async Task<int> OutboxRows()
    {
        await using var context = fixture.CreateContext();
        return await context.OutboxEvents.CountAsync();
    }

    private static RealtimeNotification Push(Guid[] recipients) => new()
    {
        NotificationId = Guid.NewGuid(),
        EventType = EventType.NewTopic,
        CreatedUtc = DateTimeOffset.UtcNow,
        RecipientIds = recipients,
        Metadata = new { marker = "push-delivery" }
    };

    // The unchanged handler with only the store of notifications and the other channels mocked.
    private static NotificationProcessor Processor(NpgsqlDataSource dataSource, Guid notificationId)
    {
        var request = new CreateNotification
        {
            EventType = EventType.NewTopic,
            UsersInterested = [NotificationSeed.MasterId],
            Metadata = new { marker = "push-delivery" },
        };

        var generator = Substitute.For<INotificationGenerator>();
        generator.CanResolve(Arg.Any<EventType>()).Returns(true);
        generator.Generate(Arg.Any<Guid>()).Returns(NotificationSeed.One(request));

        var service = Substitute.For<INotificationService>();
        service.CreateAsync(Arg.Any<IEnumerable<CreateNotification>>(), Arg.Any<CancellationToken>())
            .Returns([new CreatedNotification(request, new CreateNotificationEntity
            {
                NotificationId = notificationId,
                CreatedUtc = DateTimeOffset.UtcNow,
                EventType = EventType.NewTopic,
                UsersInterested = [NotificationSeed.MasterId],
                Metadata = request.Metadata,
            })]);

        return new NotificationProcessor(
            [generator],
            service,
            Substitute.For<INotificationEmailSender>(),
            Substitute.For<INotificationBotSender>(),
            new RealtimeNotificationProducer(dataSource),
            NullLogger<NotificationProcessor>.Instance);
    }

    /// <summary>A connection of its own that listens on the push channel and keeps what it hears.</summary>
    private sealed class TestListener : IAsyncDisposable
    {
        private readonly NpgsqlConnection _connection;
        private readonly CancellationTokenSource _stop = new();
        private readonly Task _reading;

        private TestListener(NpgsqlConnection connection)
        {
            _connection = connection;
            _connection.Notification += (_, e) => Payloads.Add(e.Payload);
            _reading = Task.Run(async () =>
            {
                try
                {
                    while (!_stop.IsCancellationRequested)
                    {
                        await _connection.WaitAsync(_stop.Token);
                    }
                }
                catch (OperationCanceledException)
                {
                }
            });
        }

        public ConcurrentBag<string> Payloads { get; } = [];

        public static async Task<TestListener> Start(string connectionString)
        {
            var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            await using (var listen = new NpgsqlCommand($"LISTEN {QueueNames.PushChannel}", connection))
            {
                await listen.ExecuteNonQueryAsync();
            }

            return new TestListener(connection);
        }

        public async Task<IReadOnlyList<RealtimeNotification>> HearPushes(Guid notificationId, int expectedRecipients)
        {
            var deadline = DateTime.UtcNow + HeardWithin;
            while (DateTime.UtcNow < deadline)
            {
                var heard = Heard(notificationId);
                if (heard.Sum(push => push.RecipientIds.Count()) >= expectedRecipients)
                {
                    // A moment more, so a duplicate piece would be heard too.
                    await Task.Delay(200);
                    return Heard(notificationId);
                }

                await Task.Delay(50);
            }

            throw new TimeoutException($"No push of {notificationId} to {expectedRecipients} recipients within {HeardWithin}");
        }

        private List<RealtimeNotification> Heard(Guid notificationId) => Payloads
            .Select(payload => PushPayload.TryRead(payload, out var push) ? push : null)
            .Where(push => push?.NotificationId == notificationId)
            .Select(push => push!)
            .ToList();

        public async ValueTask DisposeAsync()
        {
            await _stop.CancelAsync();
            await _reading;
            await _connection.DisposeAsync();
            _stop.Dispose();
        }
    }
}
