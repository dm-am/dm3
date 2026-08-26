using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Blacklists;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Identity;
using DM.Domain.Personal.Features.Notifications;
using DM.Infrastructure.Messaging.GeneralBus;
using DM.Infrastructure.Persistence;
using DM.Infrastructure.Persistence.Entities.Personal.Notifications;
using DM.Workers.NotificationDispatcher.Bot;
using DM.Workers.NotificationDispatcher.Dispatching;
using DM.Workers.NotificationDispatcher.Email;
using DM.Workers.NotificationDispatcher.Notifiers;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Npgsql;
using Xunit;

namespace DM.Workers.NotificationDispatcher.Tests;

/// <summary>
/// A broker redelivery of one event costs the recipient nothing twice.
/// </summary>
/// <remarks>
/// The bus delivers at least once: a worker killed between the write and the
/// acknowledgement, a nack, a restart - each hands the same message in again.
/// Before W1.4 that message carried no identity of its own, so a replay wrote a
/// second notification and mailed a second letter; now it carries the EventId
/// its publisher minted, the write is idempotent by (EventId, EventType), and
/// every channel is built from what was actually written this time.
///
/// Asserted through the real service, repository and schema rather than mocks
/// of them, because the guarantee lives in three places at once - the lookup,
/// the answer the channels iterate, and the unique index underneath - and a
/// mock of any one of them would pass with the other two broken.
/// </remarks>
[Collection(NotificationDatabaseCollection.Name)]
public class EventReplayShould
{
    private readonly NotificationDatabaseFixture _fixture;

    public EventReplayShould(NotificationDatabaseFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task DeliverOnceWhenTheBrokerHandsTheSameEventTwice()
    {
        var message = new InvokedEvent
        {
            Type = EventType.NewTopic,
            EntityId = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
        };

        var email = Substitute.For<INotificationEmailSender>();
        var bot = Substitute.For<INotificationBotSender>();
        var realtime = Substitute.For<IRealtimeNotificationProducer>();

        // A scope per delivered message, the way the consumer opens one
        await ProcessInFreshScope(message, email, bot, realtime);
        await ProcessInFreshScope(message, email, bot, realtime);

        await using var context = _fixture.CreateContext();
        var stored = await context.Notifications
            .Where(n => n.EventId == message.EventId)
            .Include(n => n.Recipients)
            .ToListAsync();

        var notification = stored.Should().ContainSingle(
            "the second delivery is a replay of the first, not a second event").Subject;
        notification.Recipients.Select(r => r.UserId).Should().Equal(new[] { NotificationSeed.MasterId },
            "a replay must not add a second recipient row either");

        // The letter about an already-stored notification went out with the first
        // delivery, and so did the bot message; the push too, because the channels
        // iterate what this delivery wrote, which is nothing.
        await email.Received(1).SendIfEnabled(
            Arg.Any<CreateNotification>(), Arg.Any<EventType>(), Arg.Any<CancellationToken>());
        await bot.Received(1).SendIfEnabled(
            Arg.Any<CreateNotification>(), Arg.Any<EventType>(), Arg.Any<CancellationToken>());
        await realtime.Received(1).SendAsync(
            Arg.Any<RealtimeNotification>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The invariant is a constraint of the schema, not only a habit of the
    /// code: when the lookup races a concurrent write, the index refuses the
    /// second row and the refusal replays the message into the lookup again.
    /// </summary>
    [Fact]
    public async Task HoldOneRowPerEventAndTypeAsAConstraint()
    {
        var eventId = Guid.NewGuid();
        await using var context = _fixture.CreateContext();
        context.Notifications.AddRange(
            Row(eventId, EventType.NewTopic),
            Row(eventId, EventType.NewTopic));

        var writing = async () => await context.SaveChangesAsync();

        (await writing.Should().ThrowAsync<DbUpdateException>(
                "two rows under one (EventId, EventType) are one logical notification stored twice"))
            .WithInnerException<PostgresException>()
            .Which.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);
    }

    /// <summary>
    /// The index is partial on purpose: rows born from messages that predate
    /// the key all carry NULL, and NULL marks "nothing to deduplicate against",
    /// not "the same event".
    /// </summary>
    [Fact]
    public async Task LetRowsFromThePreEventIdEraCoexist()
    {
        await using var context = _fixture.CreateContext();
        context.Notifications.AddRange(
            Row(eventId: null, EventType.NewMessage),
            Row(eventId: null, EventType.NewMessage));

        var writing = async () => await context.SaveChangesAsync();

        await writing.Should().NotThrowAsync(
            "two legacy notifications of one type are two notifications, not a replay of one");
    }

    /// <summary>
    /// The processor is where the wire spelling of "no key" becomes the domain
    /// spelling: Guid.Empty is what the codec answers for a message queued
    /// before the field existed, and stamping it through as a value would make
    /// every legacy message a replay of one and the same event.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HandTheServiceTheEventIdOnlyWhenTheMessageCarriesOne(bool carriesKey)
    {
        var eventId = carriesKey ? Guid.NewGuid() : Guid.Empty;
        IReadOnlyList<CreateNotification>? handed = null;

        var service = Substitute.For<INotificationService>();
        service
            .CreateAsync(Arg.Any<IEnumerable<CreateNotification>>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<CreatedNotification>())
            .AndDoes(ci =>
                {
                    var n = ci.ArgAt<IEnumerable<CreateNotification>>(0);
                    handed = n.ToArray();
                });

        var generator = Substitute.For<INotificationGenerator>();
        generator.CanResolve(Arg.Any<EventType>()).Returns(true);
        generator.Generate(Arg.Any<Guid>()).Returns(One(new CreateNotification
        {
            UsersInterested = [Guid.NewGuid()],
            Metadata = new object(),
        }));

        var processor = new NotificationProcessor(
            [generator],
            service,
            Substitute.For<INotificationEmailSender>(),
            Substitute.For<INotificationBotSender>(),
            Substitute.For<IRealtimeNotificationProducer>(),
            Substitute.For<ILogger<NotificationProcessor>>());

        await processor.Process("key",
            new InvokedEvent { Type = EventType.NewTopic, EntityId = Guid.NewGuid(), EventId = eventId },
            CancellationToken.None);

        handed.Should().NotBeNull();
        handed!.Should().ContainSingle().Which.EventId.Should().Be(
            carriesKey ? eventId : (Guid?)null,
            carriesKey
                ? "the key is what the idempotent write deduplicates by"
                : "a pre-EventId message has nothing to deduplicate against and must not share Empty as a key");
    }

    /// <summary>
    /// One pass of the real pipeline - processor, service, repository, schema -
    /// with only the channels and the event source mocked, in a scope of its
    /// own the way the consumer opens one per delivered message.
    /// </summary>
    private async Task ProcessInFreshScope(
        InvokedEvent message,
        INotificationEmailSender email,
        INotificationBotSender bot,
        IRealtimeNotificationProducer realtime)
    {
        await using var context = _fixture.CreateContext();

        var generator = Substitute.For<INotificationGenerator>();
        generator.CanResolve(Arg.Any<EventType>()).Returns(true);
        generator.Generate(message.EntityId).Returns(One(new CreateNotification
        {
            UsersInterested = [NotificationSeed.MasterId],
            Metadata = new { Marker = "replay-suite" },
        }));

        var processor = new NotificationProcessor(
            [generator],
            RealNotificationService(context),
            email,
            bot,
            realtime,
            Substitute.For<ILogger<NotificationProcessor>>());

        var result = await processor.Process("key", message, CancellationToken.None);
        result.Should().Be(DM.Infrastructure.Messaging.ProcessResult.Success);
    }

    /// <summary>
    /// The service the worker resolves at run time, composed here by hand. The
    /// concrete types are internal to their own assemblies and this suite is
    /// not on their InternalsVisibleTo lists, so they are reached the way the
    /// container reaches them - by name - and a rename fails with the name.
    /// </summary>
    private static INotificationService RealNotificationService(DmDbContext context)
    {
        var guidFactory = Substitute.For<IGuidFactory>();
        guidFactory.Create().Returns(_ => Guid.NewGuid());

        var repository = (INotificationRepository)Instantiate(
            typeof(DmDbContext).Assembly,
            "DM.Infrastructure.Persistence.Repositories.Personal.NotificationRepository",
            context);
        // Kept as object: the factory's interface is internal to the domain
        // assembly, but the service's constructor matches it by runtime type
        var factory = Instantiate(
            typeof(INotificationService).Assembly,
            "DM.Domain.Personal.Features.Notifications.NotificationFactory",
            guidFactory);

        // The identity provider is never consulted on the create path - the
        // worker itself registers one that refuses to answer - and the
        // blacklist stays out of the way because the notification names no
        // actor to filter by. The logger is built through the generic type the
        // same way the service itself is reached: ILogger<T> over an internal
        // T cannot be spelled from this assembly.
        var serviceType = typeof(INotificationService).Assembly
            .GetType("DM.Domain.Personal.Features.Notifications.NotificationService")!;
        var logger = Activator.CreateInstance(
            typeof(Logger<>).MakeGenericType(serviceType),
            NullLoggerFactory.Instance)!;
        return (INotificationService)Instantiate(
            typeof(INotificationService).Assembly,
            "DM.Domain.Personal.Features.Notifications.NotificationService",
            Substitute.For<IIdentityProvider>(),
            EveryNotificationGeneratorShould.ClockAt(DateTimeOffset.UtcNow),
            factory,
            repository,
            Substitute.For<IUserBlacklistChecker>(),
            logger);
    }

    private static object Instantiate(Assembly assembly, string typeName, params object[] args)
    {
        var type = assembly.GetType(typeName);
        type.Should().NotBeNull(
            $"{typeName} is what the worker's container resolves, and a rename must land here too");
        return Activator.CreateInstance(type!, args)!;
    }

    /// <summary>A stored row with no recipients: enough to trip or miss the index.</summary>
    private static Notification Row(Guid? eventId, EventType eventType) => new()
    {
        NotificationId = Guid.NewGuid(),
        EventId = eventId,
        EventType = eventType,
        CreatedUtc = DateTimeOffset.UtcNow,
        Metadata = "{}",
    };

    private static async IAsyncEnumerable<CreateNotification> One(CreateNotification notification)
    {
        yield return notification;
        await Task.CompletedTask;
    }
}
