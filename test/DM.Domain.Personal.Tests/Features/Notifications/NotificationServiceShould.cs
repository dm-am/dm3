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
using FluentAssertions;
using Moq;
using Xunit;

namespace DM.Domain.Personal.Tests.Features.Notifications;

public class NotificationServiceShould : UnitTestBase
{
    private readonly Mock<IIdentityProvider> _identityProvider;
    private readonly Mock<IDateTimeProvider> _dateTimeProvider;
    private readonly Mock<INotificationFactory> _factory;
    private readonly Mock<INotificationRepository> _repository;
    private readonly Mock<IUserBlacklistChecker> _blacklistChecker;
    private readonly NotificationService _service;
    private readonly Guid _currentUserId = Guid.NewGuid();
    private readonly DateTimeOffset _now = DateTimeOffset.UtcNow;

    public NotificationServiceShould()
    {
        _identityProvider = Mock<IIdentityProvider>();
        _dateTimeProvider = Mock<IDateTimeProvider>();
        _factory = Mock<INotificationFactory>();
        _repository = Mock<INotificationRepository>();
        _blacklistChecker = Mock<IUserBlacklistChecker>();

        var settings = new UserSettings
        {
            Paging = new PagingSettings { EntitiesPerPage = 10 }
        };
        var identity = Identities.User(_currentUserId, "CurrentUser", UserRole.RegularUser, settings);
        _identityProvider.Setup(p => p.Current).Returns(identity);
        _dateTimeProvider.Setup(d => d.Now).Returns(_now);

        _service = new NotificationService(
            _identityProvider.Object,
            _dateTimeProvider.Object,
            _factory.Object,
            _repository.Object,
            _blacklistChecker.Object);
    }

    [Fact]
    public async Task CountUnreadNotificationsForCurrentUser()
    {
        _repository.Setup(r => r.CountUnread(_currentUserId))
            .ReturnsAsync(5);

        var result = await _service.CountUnreadAsync();

        result.Should().Be(5);
    }

    [Fact]
    public async Task GetNotificationsWithPaging()
    {
        _repository.Setup(r => r.Count(_currentUserId))
            .ReturnsAsync(25);
        _repository.Setup(r => r.GetNotifications(_currentUserId, It.IsAny<PagingData>()))
            .ReturnsAsync([]);

        var query = new PagingQuery { Skip = 0, Take = 10 };
        var result = await _service.GetAsync(query);

        result.Should().NotBeNull();
        _repository.Verify(r => r.GetNotifications(
            _currentUserId,
            It.Is<PagingData>(p => p.Skip == 0 && p.Take == 10)), Times.Once);
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

        _factory.Setup(f => f.Create(It.IsAny<CreateNotification>(), _now))
            .Returns<CreateNotification, DateTimeOffset>((n, d) =>
                new CreateNotificationEntity
                {
                    UsersInterested = n.UsersInterested,
                    EventType = n.EventType,
                    Metadata = n.Metadata
                });

        _repository.Setup(r => r.Create(It.IsAny<CreateNotificationEntity[]>()))
            .Returns(Task.CompletedTask);

        await _service.CreateAsync(createNotifications);

        _factory.Verify(f => f.Create(It.IsAny<CreateNotification>(), _now), Times.Exactly(2));
        _repository.Verify(r => r.Create(It.IsAny<CreateNotificationEntity[]>()), Times.Once);
    }

    [Fact]
    public async Task NotStoreNotificationsNobodyCanRead()
    {
        var addressedId = Guid.NewGuid();
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

        _factory.Setup(f => f.Create(It.IsAny<CreateNotification>(), _now))
            .Returns<CreateNotification, DateTimeOffset>((n, d) =>
                new CreateNotificationEntity
                {
                    NotificationId = n.UsersInterested.Any() ? addressedId : Guid.NewGuid(),
                    UsersInterested = n.UsersInterested,
                    EventType = n.EventType,
                    Metadata = n.Metadata
                });

        CreateNotificationEntity[]? stored = null;
        _repository.Setup(r => r.Create(It.IsAny<CreateNotificationEntity[]>()))
            .Callback<IEnumerable<CreateNotificationEntity>>(n => stored = n.ToArray())
            .Returns(Task.CompletedTask);

        var result = (await _service.CreateAsync(createNotifications)).ToArray();

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

        _factory.Setup(f => f.Create(It.IsAny<CreateNotification>(), _now))
            .Returns<CreateNotification, DateTimeOffset>((n, d) =>
                new CreateNotificationEntity
                {
                    NotificationId = Guid.NewGuid(),
                    UsersInterested = n.UsersInterested,
                    EventType = n.EventType,
                    Metadata = n.Metadata
                });

        CreateNotificationEntity[]? stored = null;
        _repository.Setup(r => r.Create(It.IsAny<CreateNotificationEntity[]>()))
            .Callback<IEnumerable<CreateNotificationEntity>>(n => stored = n.ToArray())
            .Returns(Task.CompletedTask);

        var result = (await _service.CreateAsync(createNotifications)).ToArray();

        stored.Should().NotBeNull();
        stored!.Should().ContainSingle().Which.EventType.Should().Be(EventType.LikedMessage);

        // Both entities come back: the caller pushes them and needs the ids
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task NotTouchTheRepositoryWhenNoNotificationHasRecipients()
    {
        _factory.Setup(f => f.Create(It.IsAny<CreateNotification>(), _now))
            .Returns<CreateNotification, DateTimeOffset>((n, d) =>
                new CreateNotificationEntity
                {
                    NotificationId = Guid.NewGuid(),
                    UsersInterested = n.UsersInterested,
                    EventType = n.EventType,
                    Metadata = n.Metadata
                });

        var result = await _service.CreateAsync([
            new CreateNotification
            {
                EventType = EventType.NewGlobalChatMessage,
                UsersInterested = []
            }
        ]);

        _repository.Verify(r => r.Create(It.IsAny<CreateNotificationEntity[]>()), Times.Never);
        result.Should().HaveCount(1);
    }

    /// <summary>
    /// Blocking somebody is a statement about hearing from them at all, and the
    /// notification list, the mail and the bots are where the site speaks
    /// loudest. Two recipients, one of whom blocked the actor: the other still
    /// gets it.
    /// </summary>
    [Fact]
    public async Task NotAddressARecipientWhoBlockedTheActor()
    {
        var actorId = Guid.NewGuid();
        var blocker = Guid.NewGuid();
        var bystander = Guid.NewGuid();

        _blacklistChecker
            .Setup(c => c.GetOwnersBlockingAsync(actorId, It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<Guid> { blocker });

        PassThroughFactory();
        CreateNotificationEntity[]? stored = null;
        _repository.Setup(r => r.Create(It.IsAny<CreateNotificationEntity[]>()))
            .Callback<IEnumerable<CreateNotificationEntity>>(n => stored = n.ToArray())
            .Returns(Task.CompletedTask);

        var result = await _service.CreateAsync([
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
        _repository.Setup(r => r.Create(It.IsAny<CreateNotificationEntity[]>()))
            .Returns(Task.CompletedTask);

        await _service.CreateAsync([
            new CreateNotification
            {
                EventType = EventType.GameClosureWarning,
                UsersInterested = [Guid.NewGuid()]
            }
        ]);

        _blacklistChecker.Verify(
            c => c.GetOwnersBlockingAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// One statement per actor, not per recipient: a publication notifies every
    /// subscriber of the blog, and a read each would put the whole audience into
    /// the query count of one event.
    /// </summary>
    [Fact]
    public async Task AskTheBlacklistOnceForTheWholeAudienceOfAnActor()
    {
        var actorId = Guid.NewGuid();
        _blacklistChecker
            .Setup(c => c.GetOwnersBlockingAsync(actorId, It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<Guid>());

        PassThroughFactory();
        _repository.Setup(r => r.Create(It.IsAny<CreateNotificationEntity[]>()))
            .Returns(Task.CompletedTask);

        await _service.CreateAsync([
            new CreateNotification
            {
                EventType = EventType.NewPublication,
                ActorId = actorId,
                UsersInterested = [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()]
            }
        ]);

        _blacklistChecker.Verify(
            c => c.GetOwnersBlockingAsync(actorId, It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>Entity that mirrors the request it was built from.</summary>
    private void PassThroughFactory() =>
        _factory.Setup(f => f.Create(It.IsAny<CreateNotification>(), _now))
            .Returns<CreateNotification, DateTimeOffset>((n, _) => new CreateNotificationEntity
            {
                NotificationId = Guid.NewGuid(),
                UsersInterested = n.UsersInterested,
                EventType = n.EventType,
                Metadata = n.Metadata
            });

    [Fact]
    public async Task MarkSingleNotificationAsRead()
    {
        var notificationId = Guid.NewGuid();
        _repository.Setup(r => r.MarkAsRead(notificationId, _currentUserId))
            .Returns(Task.CompletedTask);

        await _service.MarkAsReadAsync(notificationId);

        _repository.Verify(r => r.MarkAsRead(notificationId, _currentUserId), Times.Once);
    }

    [Fact]
    public async Task MarkAllNotificationsAsRead()
    {
        _repository.Setup(r => r.MarkAsRead(_currentUserId))
            .Returns(Task.CompletedTask);

        await _service.MarkAllAsReadAsync();

        _repository.Verify(r => r.MarkAsRead(_currentUserId), Times.Once);
    }
}
