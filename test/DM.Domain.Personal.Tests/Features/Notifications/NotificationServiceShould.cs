using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Blacklists;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Identity;
using DM.Domain.Personal.Features.Notifications;
using DM.Testing.Dsl;
using DM.Testing;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace DM.Domain.Personal.Tests.Features.Notifications;

public class NotificationServiceShould : UnitTestBase
{
    private readonly IIdentityProvider identityProvider;
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly INotificationFactory factory;
    private readonly INotificationRepository repository;
    private readonly IUserBlacklistChecker blacklistChecker;
    private readonly NotificationService service;
    private readonly Guid currentUserId = Guid.NewGuid();
    private readonly DateTimeOffset now = DateTimeOffset.UtcNow;

    public NotificationServiceShould()
    {
        identityProvider = Mock<IIdentityProvider>();
        dateTimeProvider = Mock<IDateTimeProvider>();
        factory = Mock<INotificationFactory>();
        repository = Mock<INotificationRepository>();
        blacklistChecker = Mock<IUserBlacklistChecker>();

        var settings = new UserSettings { PageSize = 10 };
        var identity = Identities.User(currentUserId, "CurrentUser", UserRole.RegularUser, settings);
        identityProvider.Current.Returns(identity);
        dateTimeProvider.Now.Returns(now);

        service = new NotificationService(
            identityProvider,
            dateTimeProvider,
            factory,
            repository,
            blacklistChecker,
            NullLogger<NotificationService>.Instance);
    }

    [Fact]
    public async Task CountUnreadNotificationsForCurrentUser()
    {
        repository.CountUnread(currentUserId).Returns(5);

        var result = await service.CountUnreadAsync();

        result.Should().Be(5);
    }

    [Fact]
    public async Task GetNotificationsWithPaging()
    {
        repository.Count(currentUserId).Returns(25);
        repository.GetNotifications(currentUserId, Arg.Any<PagingData>()).Returns([]);

        var query = new PagingQuery { Skip = 0, Take = 10 };
        await service.GetAsync(query);

        await repository.Received(1).GetNotifications(
            currentUserId,
            Arg.Is<PagingData>(p => p.Skip == 0 && p.Take == 10));
    }

    [Fact]
    public async Task ServeTheReadersPageSizeWhenTheQueryNamesNone()
    {
        repository.Count(currentUserId).Returns(25);
        repository.GetNotifications(currentUserId, Arg.Any<PagingData>()).Returns([]);

        var (_, paging) = await service.GetAsync(new PagingQuery { Skip = 0, Take = 0 });

        paging.PageSize.Should().Be(10);
    }

    [Fact]
    public async Task CreateNotificationsWithCorrectDate()
    {
        var createNotifications = new[]
        {
            new CreateNotification
            {
                UsersInterested = new[] { Guid.NewGuid() },
                EventType = EventType.NewMessage,
                Metadata = "Notification 1"
            },
            new CreateNotification
            {
                UsersInterested = new[] { Guid.NewGuid() },
                EventType = EventType.NewMessage,
                Metadata = "Notification 2"
            }
        };

        PassThroughFactory();

        repository.Create(Arg.Any<CreateNotificationEntity[]>()).Returns(Task.CompletedTask);

        await service.CreateAsync(createNotifications);

        factory.Received(2).Create(Arg.Any<CreateNotification>(), now);
        await repository.Received(1).Create(Arg.Any<CreateNotificationEntity[]>());
    }

    [Fact]
    public async Task NotStoreNotificationsNobodyCanRead()
    {
        var createNotifications = new[]
        {
            new CreateNotification
            {
                EventType = EventType.NewMessage,
                UsersInterested = [Guid.NewGuid()]
            },
            // Global chat fans out through the realtime hub, so it produces a
            // notification with no recipients on purpose
            new CreateNotification
            {
                EventType = EventType.NewGlobalChatMessage,
                UsersInterested = []
            }
        };

        PassThroughFactory();

        CreateNotificationEntity[]? stored = null;
        repository.Create(Arg.Do<IEnumerable<CreateNotificationEntity>>(n => stored = n.ToArray()))
            .Returns(Task.CompletedTask);

        var result = (await service.CreateAsync(createNotifications)).ToArray();

        stored.Should().NotBeNull();
        stored!.Should().HaveCount(1);
        stored![0].EventType.Should().Be(EventType.NewMessage);

        // Both entities come back: the caller broadcasts them and needs the ids
        result.Should().HaveCount(2);
        result.Should().OnlyContain(n => n.Entity.NotificationId != Guid.Empty);
    }

    [Fact]
    public async Task NotStoreRealtimeOnlyNotifications()
    {
        var createNotifications = new[]
        {
            new CreateNotification
            {
                EventType = EventType.LikedMessage,
                UsersInterested = [Guid.NewGuid()]
            },
            // A realtime-only notification is addressed and still not stored: it
            // nudges an open tab to re-read a counter, while a stored row would
            // land in the notification list and in the mail queue
            new CreateNotification
            {
                EventType = EventType.NewMessage,
                UsersInterested = [Guid.NewGuid()],
                RealtimeOnly = true
            }
        };

        PassThroughFactory();

        CreateNotificationEntity[]? stored = null;
        repository.Create(Arg.Do<IEnumerable<CreateNotificationEntity>>(n => stored = n.ToArray()))
            .Returns(Task.CompletedTask);

        var result = (await service.CreateAsync(createNotifications)).ToArray();

        stored.Should().NotBeNull();
        stored!.Should().ContainSingle().Which.EventType.Should().Be(EventType.LikedMessage);

        // Both entities come back: the caller pushes them and needs the ids
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task NotTouchTheRepositoryWhenNoNotificationHasRecipients()
    {
        PassThroughFactory();

        var result = await service.CreateAsync([
            new CreateNotification
            {
                EventType = EventType.NewGlobalChatMessage,
                UsersInterested = []
            }
        ]);

        await repository.DidNotReceive().Create(Arg.Any<CreateNotificationEntity[]>());
        result.Should().HaveCount(1);
    }

    // Blocking somebody is a statement about hearing from them at all, the other recipient still
    // gets it.
    [Fact]
    public async Task NotAddressARecipientWhoBlockedTheActor()
    {
        var actorId = Guid.NewGuid();
        var blocker = Guid.NewGuid();
        var bystander = Guid.NewGuid();

        blacklistChecker
            .GetOwnersBlockingAsync(actorId, Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new HashSet<Guid> { blocker });

        PassThroughFactory();
        CreateNotificationEntity[]? stored = null;
        repository.Create(Arg.Do<IEnumerable<CreateNotificationEntity>>(n => stored = n.ToArray()))
            .Returns(Task.CompletedTask);

        var result = await service.CreateAsync([
            new CreateNotification
            {
                EventType = EventType.LikedBlogComment,
                ActorId = actorId,
                UsersInterested = [blocker, bystander]
            }
        ]);

        stored!.Single().UsersInterested.Should().Equal(bystander);

        // The answer is what every channel is built from, so the filtered
        // audience has to be on the request too: mail reads it from there.
        result.Single().Source.UsersInterested.Should().Equal(bystander);
    }

    /// <summary>
    /// A notification nobody did — a deadline, a reminder — has no actor, and an
    /// unconditional filter would need a blacklist read it cannot key.
    /// </summary>
    [Fact]
    public async Task NotConsultTheBlacklistForANotificationWithNoActor()
    {
        PassThroughFactory();
        repository.Create(Arg.Any<CreateNotificationEntity[]>()).Returns(Task.CompletedTask);

        await service.CreateAsync([
            new CreateNotification
            {
                EventType = EventType.GameClosureWarning,
                UsersInterested = [Guid.NewGuid()]
            }
        ]);

        await blacklistChecker.DidNotReceive().GetOwnersBlockingAsync(Arg.Any<Guid>(), Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());
    }

    // One read per actor, not per recipient: a publication notifies every subscriber of the blog.
    [Fact]
    public async Task AskTheBlacklistOnceForTheWholeAudienceOfAnActor()
    {
        var actorId = Guid.NewGuid();
        blacklistChecker
            .GetOwnersBlockingAsync(actorId, Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new HashSet<Guid>());

        PassThroughFactory();
        repository.Create(Arg.Any<CreateNotificationEntity[]>()).Returns(Task.CompletedTask);

        await service.CreateAsync([
            new CreateNotification
            {
                EventType = EventType.NewPublication,
                ActorId = actorId,
                UsersInterested = [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()]
            }
        ]);

        await blacklistChecker.Received(1).GetOwnersBlockingAsync(actorId, Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());
    }

    // The bus delivers at least once, and the pair (EventId, EventType) names one logical
    // notification.
    [Fact]
    public async Task DropNotificationsAlreadyStoredForTheSameEvent()
    {
        var eventId = Guid.NewGuid();
        repository.GetCreatedEventTypes(eventId).Returns(new HashSet<EventType> { EventType.NewTopic });

        PassThroughFactory();
        CreateNotificationEntity[]? stored = null;
        repository.Create(Arg.Do<IEnumerable<CreateNotificationEntity>>(n => stored = n.ToArray()))
            .Returns(Task.CompletedTask);

        var result = await service.CreateAsync([
            new CreateNotification
            {
                EventType = EventType.NewTopic,
                EventId = eventId,
                UsersInterested = [Guid.NewGuid()]
            },
            new CreateNotification
            {
                EventType = EventType.NewTopicFromSubscribedAuthor,
                EventId = eventId,
                UsersInterested = [Guid.NewGuid()]
            }
        ]);

        stored!.Should().ContainSingle(
                "the subscription fan-out of this event was not stored yet, so the replay owes it")
            .Which.EventType.Should().Be(EventType.NewTopicFromSubscribedAuthor);
        result.Should().ContainSingle(
            "the answer is what every channel is built from, and a notification the first " +
            "delivery stored has been mailed by the first delivery too");
    }

    /// <summary>
    /// The whole batch already stored: the replay writes nothing, and the empty
    /// answer is what keeps every channel of the caller silent.
    /// </summary>
    [Fact]
    public async Task AnswerNothingWhenTheWholeEventWasAlreadyStored()
    {
        var eventId = Guid.NewGuid();
        repository.GetCreatedEventTypes(eventId).Returns(new HashSet<EventType> { EventType.NewMessage });

        var result = await service.CreateAsync([
            new CreateNotification
            {
                EventType = EventType.NewMessage,
                EventId = eventId,
                UsersInterested = [Guid.NewGuid()]
            }
        ]);

        result.Should().BeEmpty();
        await repository.DidNotReceive().Create(Arg.Any<CreateNotificationEntity[]>());
    }

    [Fact]
    public async Task NotConsultTheStoreForANotificationWithoutEventId()
    {
        PassThroughFactory();
        repository.Create(Arg.Any<CreateNotificationEntity[]>()).Returns(Task.CompletedTask);

        var result = await service.CreateAsync([
            new CreateNotification
            {
                EventType = EventType.NewMessage,
                UsersInterested = [Guid.NewGuid()]
            }
        ]);

        await repository.DidNotReceive().GetCreatedEventTypes(Arg.Any<Guid>());
        await repository.Received(1).Create(Arg.Any<CreateNotificationEntity[]>());
        result.Should().ContainSingle();
    }

    private void PassThroughFactory() =>
        factory.Create(Arg.Any<CreateNotification>(), now).Returns(ci =>
        {
            var n = ci.ArgAt<CreateNotification>(0);
            return new CreateNotificationEntity
            {
                NotificationId = Guid.NewGuid(),
                UsersInterested = n.UsersInterested,
                EventType = n.EventType,
                Metadata = n.Metadata
            };
        });

    [Fact]
    public async Task MarkSingleNotificationAsRead()
    {
        var notificationId = Guid.NewGuid();
        repository.MarkAsRead(notificationId, currentUserId).Returns(Task.CompletedTask);

        await service.MarkAsReadAsync(notificationId);

        await repository.Received(1).MarkAsRead(notificationId, currentUserId);
    }

    [Fact]
    public async Task MarkAllNotificationsAsRead()
    {
        repository.MarkAsRead(currentUserId).Returns(Task.CompletedTask);

        await service.MarkAllAsReadAsync();

        await repository.Received(1).MarkAsRead(currentUserId);
    }
}
