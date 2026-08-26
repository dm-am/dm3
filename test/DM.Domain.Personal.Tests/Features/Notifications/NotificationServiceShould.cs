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
using NSubstitute;
using Xunit;

using Microsoft.Extensions.Logging.Abstractions;
namespace DM.Domain.Personal.Tests.Features.Notifications;

public class NotificationServiceShould : UnitTestBase
{
    private readonly IIdentityProvider _identityProvider;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly INotificationFactory _factory;
    private readonly INotificationRepository _repository;
    private readonly IUserBlacklistChecker _blacklistChecker;
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
        _identityProvider.Current.Returns(identity);
        _dateTimeProvider.Now.Returns(_now);

        _service = new NotificationService(
            _identityProvider,
            _dateTimeProvider,
            _factory,
            _repository,
            _blacklistChecker,
            NullLogger<NotificationService>.Instance);
    }

    [Fact]
    public async Task CountUnreadNotificationsForCurrentUser()
    {
        _repository.CountUnread(_currentUserId).Returns(5);

        var result = await _service.CountUnreadAsync();

        result.Should().Be(5);
    }

    [Fact]
    public async Task GetNotificationsWithPaging()
    {
        _repository.Count(_currentUserId).Returns(25);
        _repository.GetNotifications(_currentUserId, Arg.Any<PagingData>()).Returns([]);

        var query = new PagingQuery { Skip = 0, Take = 10 };
        var result = await _service.GetAsync(query);

        result.Should().NotBeNull();
        await _repository.Received(1).GetNotifications(
            _currentUserId,
            Arg.Is<PagingData>(p => p.Skip == 0 && p.Take == 10));
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

        _factory.Create(Arg.Any<CreateNotification>(), _now).Returns(ci =>
        {
            var n = ci.ArgAt<CreateNotification>(0); var d = ci.ArgAt<DateTimeOffset>(1); return new CreateNotificationEntity
            {
                UsersInterested = n.UsersInterested,
                EventType = n.EventType,
                Metadata = n.Metadata
            };
        });

        _repository.Create(Arg.Any<CreateNotificationEntity[]>()).Returns(Task.CompletedTask);

        await _service.CreateAsync(createNotifications);

        _factory.Received(2).Create(Arg.Any<CreateNotification>(), _now);
        await _repository.Received(1).Create(Arg.Any<CreateNotificationEntity[]>());
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

        _factory.Create(Arg.Any<CreateNotification>(), _now).Returns(ci =>
        {
            var n = ci.ArgAt<CreateNotification>(0); var d = ci.ArgAt<DateTimeOffset>(1); return new CreateNotificationEntity
            {
                NotificationId = n.UsersInterested.Any() ? addressedId : Guid.NewGuid(),
                UsersInterested = n.UsersInterested,
                EventType = n.EventType,
                Metadata = n.Metadata
            };
        });

        CreateNotificationEntity[]? stored = null;
        _repository.Create(Arg.Any<CreateNotificationEntity[]>())
            .Returns(Task.CompletedTask)
            .AndDoes(ci =>
            {
                var n = ci.ArgAt<IEnumerable<CreateNotificationEntity>>(0);
                stored = n.ToArray();
            });

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

        _factory.Create(Arg.Any<CreateNotification>(), _now).Returns(ci =>
        {
            var n = ci.ArgAt<CreateNotification>(0); var d = ci.ArgAt<DateTimeOffset>(1); return new CreateNotificationEntity
            {
                NotificationId = Guid.NewGuid(),
                UsersInterested = n.UsersInterested,
                EventType = n.EventType,
                Metadata = n.Metadata
            };
        });

        CreateNotificationEntity[]? stored = null;
        _repository.Create(Arg.Any<CreateNotificationEntity[]>())
            .Returns(Task.CompletedTask)
            .AndDoes(ci =>
            {
                var n = ci.ArgAt<IEnumerable<CreateNotificationEntity>>(0);
                stored = n.ToArray();
            });

        var result = (await _service.CreateAsync(createNotifications)).ToArray();

        stored.Should().NotBeNull();
        stored!.Should().ContainSingle().Which.EventType.Should().Be(EventType.LikedMessage);

        // Both entities come back: the caller pushes them and needs the ids
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task NotTouchTheRepositoryWhenNoNotificationHasRecipients()
    {
        _factory.Create(Arg.Any<CreateNotification>(), _now).Returns(ci =>
        {
            var n = ci.ArgAt<CreateNotification>(0); var d = ci.ArgAt<DateTimeOffset>(1); return new CreateNotificationEntity
            {
                NotificationId = Guid.NewGuid(),
                UsersInterested = n.UsersInterested,
                EventType = n.EventType,
                Metadata = n.Metadata
            };
        });

        var result = await _service.CreateAsync([
            new CreateNotification
            {
                EventType = EventType.NewGlobalChatMessage,
                UsersInterested = []
            }
        ]);

        await _repository.DidNotReceive().Create(Arg.Any<CreateNotificationEntity[]>());
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
            .GetOwnersBlockingAsync(actorId, Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new HashSet<Guid> { blocker });

        PassThroughFactory();
        CreateNotificationEntity[]? stored = null;
        _repository.Create(Arg.Any<CreateNotificationEntity[]>())
            .Returns(Task.CompletedTask)
            .AndDoes(ci =>
            {
                var n = ci.ArgAt<IEnumerable<CreateNotificationEntity>>(0);
                stored = n.ToArray();
            });

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
        _repository.Create(Arg.Any<CreateNotificationEntity[]>()).Returns(Task.CompletedTask);

        await _service.CreateAsync([
            new CreateNotification
            {
                EventType = EventType.GameClosureWarning,
                UsersInterested = [Guid.NewGuid()]
            }
        ]);

        await _blacklistChecker.DidNotReceive().GetOwnersBlockingAsync(Arg.Any<Guid>(), Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());
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
            .GetOwnersBlockingAsync(actorId, Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new HashSet<Guid>());

        PassThroughFactory();
        _repository.Create(Arg.Any<CreateNotificationEntity[]>()).Returns(Task.CompletedTask);

        await _service.CreateAsync([
            new CreateNotification
            {
                EventType = EventType.NewPublication,
                ActorId = actorId,
                UsersInterested = [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()]
            }
        ]);

        await _blacklistChecker.Received(1).GetOwnersBlockingAsync(actorId, Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The bus delivers at least once, so a redelivered event asks to create the
    /// notifications its first delivery already stored. The pair (EventId,
    /// EventType) names one logical notification; what is stored under it is
    /// dropped from both the write and the answer, so no channel repeats it.
    /// </summary>
    [Fact]
    public async Task DropNotificationsAlreadyStoredForTheSameEvent()
    {
        var eventId = Guid.NewGuid();
        _repository.GetCreatedEventTypes(eventId).Returns(new HashSet<EventType> { EventType.NewTopic });

        PassThroughFactory();
        CreateNotificationEntity[]? stored = null;
        _repository.Create(Arg.Any<CreateNotificationEntity[]>())
            .Returns(Task.CompletedTask)
            .AndDoes(ci =>
            {
                var n = ci.ArgAt<IEnumerable<CreateNotificationEntity>>(0);
                stored = n.ToArray();
            });

        var result = await _service.CreateAsync([
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
        _repository.GetCreatedEventTypes(eventId).Returns(new HashSet<EventType> { EventType.NewMessage });

        var result = await _service.CreateAsync([
            new CreateNotification
            {
                EventType = EventType.NewMessage,
                EventId = eventId,
                UsersInterested = [Guid.NewGuid()]
            }
        ]);

        result.Should().BeEmpty();
        await _repository.DidNotReceive().Create(Arg.Any<CreateNotificationEntity[]>());
    }

    /// <summary>
    /// No EventId means there is nothing to deduplicate against - a message from
    /// before the key existed - so the write proceeds without a lookup rather
    /// than treating every legacy message as a replay of one and the same event.
    /// </summary>
    [Fact]
    public async Task NotConsultTheStoreForANotificationWithoutEventId()
    {
        PassThroughFactory();
        _repository.Create(Arg.Any<CreateNotificationEntity[]>()).Returns(Task.CompletedTask);

        var result = await _service.CreateAsync([
            new CreateNotification
            {
                EventType = EventType.NewMessage,
                UsersInterested = [Guid.NewGuid()]
            }
        ]);

        await _repository.DidNotReceive().GetCreatedEventTypes(Arg.Any<Guid>());
        await _repository.Received(1).Create(Arg.Any<CreateNotificationEntity[]>());
        result.Should().ContainSingle();
    }

    /// <summary>Entity that mirrors the request it was built from.</summary>
    private void PassThroughFactory() =>
        _factory.Create(Arg.Any<CreateNotification>(), _now).Returns(ci =>
        {
            var n = ci.ArgAt<CreateNotification>(0); return new CreateNotificationEntity
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
        _repository.MarkAsRead(notificationId, _currentUserId).Returns(Task.CompletedTask);

        await _service.MarkAsReadAsync(notificationId);

        await _repository.Received(1).MarkAsRead(notificationId, _currentUserId);
    }

    [Fact]
    public async Task MarkAllNotificationsAsRead()
    {
        _repository.MarkAsRead(_currentUserId).Returns(Task.CompletedTask);

        await _service.MarkAllAsReadAsync();

        await _repository.Received(1).MarkAsRead(_currentUserId);
    }
}
