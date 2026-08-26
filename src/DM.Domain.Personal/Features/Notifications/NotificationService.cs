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
using Microsoft.Extensions.Logging;

namespace DM.Domain.Personal.Features.Notifications;

/// <inheritdoc />
internal class NotificationService : INotificationService
{
    private readonly IIdentityProvider _identityProvider;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly INotificationFactory _factory;
    private readonly INotificationRepository _repository;
    private readonly IUserBlacklistChecker _blacklistChecker;
    private readonly ILogger<NotificationService> _logger;

    /// <inheritdoc />
    public NotificationService(
        IIdentityProvider identityProvider,
        IDateTimeProvider dateTimeProvider,
        INotificationFactory factory,
        INotificationRepository repository,
        IUserBlacklistChecker blacklistChecker,
        ILogger<NotificationService> logger)
    {
        _identityProvider = identityProvider;
        _dateTimeProvider = dateTimeProvider;
        _factory = factory;
        _repository = repository;
        _blacklistChecker = blacklistChecker;
        _logger = logger;
    }

    #region Reading

    /// <inheritdoc />
    public Task<long> CountUnreadAsync() =>
        _repository.CountUnread(_identityProvider.Current.User.UserId);

    /// <inheritdoc />
    public async Task<(IEnumerable<UserNotification> Notifications, PagingResult Paging)> GetAsync(PagingQuery query)
    {
        var userId = _identityProvider.Current.User.UserId;
        var totalCount = await _repository.Count(userId);
        var pagingData = new PagingData(query, 10, (int)totalCount);
        var notifications = await _repository.GetNotifications(userId, pagingData);
        return (notifications, pagingData.Result);
    }

    #endregion

    #region Creating

    /// <inheritdoc />
    public async Task<IReadOnlyList<CreatedNotification>> CreateAsync(
        IEnumerable<CreateNotification> createNotifications, CancellationToken ct = default)
    {
        var createDate = _dateTimeProvider.Now;
        var requested = await ExcludeAlreadyCreated(
            DeduplicateWithinBatch(createNotifications.ToArray()));
        if (requested.Count == 0)
        {
            return [];
        }

        // The one place a personal blacklist reaches notifications. Applied here
        // and not in the generators because there are forty-one of them: a rule
        // written per generator is a rule the forty-second forgets, and the ones
        // that exist had no reason to know about blacklists at all.
        var addressedTo = await ExcludeBlockedRecipients(requested, ct);

        var notifications = addressedTo
            .Select(n => new CreatedNotification(n, _factory.Create(n, createDate)))
            .ToArray();

        // A notification with no recipients is not stored: nobody can ever read
        // it, so the row is unreachable by construction. Global chat produces
        // exactly this — its fan-out goes to every connected client through the
        // realtime hub, not through a per-user notification. A realtime-only one
        // is addressed and still not stored: it exists to nudge an open tab, and
        // a row would show up in the notification list and in the mail queue.
        // The entity is returned in both cases so the caller can broadcast it:
        // it needs the assigned id.
        var addressed = notifications
            .Where(n => !n.Source.RealtimeOnly && n.Entity.UsersInterested.Any())
            .Select(n => n.Entity)
            .ToArray();
        if (addressed.Length > 0)
        {
            await _repository.Create(addressed);
        }

        return notifications;
    }

    /// <summary>
    /// Drops the notifications a previous delivery of the same event already
    /// stored, keyed by (EventId, EventType).
    /// </summary>
    /// <remarks>
    /// The bus delivers at least once, so a broker redelivery hands the same
    /// publication in again with the same EventId. EventType disambiguates
    /// within it: one event fans out through several generators, each answering
    /// at most one notification of its own output type, so the pair names one
    /// logical notification and the unique index on it holds the same pair to
    /// one row when this check races a concurrent write. A notification without
    /// an EventId — a caller that predates the key — is not deduplicated: Empty
    /// would make every legacy message a replay of one and the same event.
    ///
    /// Answering the caller with the survivors only is what keeps every channel
    /// quiet on a replay: mail, bots and the realtime push are all built from
    /// this method's answer, so a notification dropped here is one no channel
    /// sends twice. What this cannot cover is a realtime-only notification —
    /// there is no row to find, so a replay pushes it again — and that is
    /// acceptable by construction: it nudges an open tab to re-read a counter,
    /// and reading it twice shows the same number.
    /// </remarks>
    /// <summary>
    /// Drops batch entries that repeat a (EventId, EventType) pair already in it.
    /// </summary>
    /// <remarks>
    /// Today no two generators answer one event with the same output type -
    /// checked across all of them during W1.4 - but the guarantee is one
    /// forgotten rename away (review of W1.4). Without this guard such a
    /// collision would not trim a row: both copies fly into one SaveChanges,
    /// the unique index refuses the whole batch on every retry, and the event
    /// dies in the dead letter queue with all channels silent. A warning makes
    /// the future mistake observable instead of fatal.
    /// </remarks>
    private IReadOnlyList<CreateNotification> DeduplicateWithinBatch(
        IReadOnlyList<CreateNotification> notifications)
    {
        var seen = new HashSet<(Guid, EventType)>();
        var kept = new List<CreateNotification>(notifications.Count);
        foreach (var notification in notifications)
        {
            if (notification.EventId is { } eventId
                && !seen.Add((eventId, notification.EventType)))
            {
                _logger.LogWarning(
                    "Two generators produced {EventType} for event {EventId}; " +
                    "the duplicate is dropped so the batch can commit",
                    notification.EventType, eventId);
                continue;
            }
            kept.Add(notification);
        }
        return kept;
    }

    private async Task<IReadOnlyList<CreateNotification>> ExcludeAlreadyCreated(
        IReadOnlyList<CreateNotification> notifications)
    {
        // Normally zero or one distinct id: a batch is one event's fan-out
        var eventIds = notifications
            .Where(n => n.EventId.HasValue)
            .Select(n => n.EventId!.Value)
            .Distinct()
            .ToArray();

        var remaining = notifications;
        foreach (var eventId in eventIds)
        {
            var created = await _repository.GetCreatedEventTypes(eventId);
            if (created.Count == 0)
            {
                continue;
            }

            remaining = remaining
                .Where(n => n.EventId != eventId || !created.Contains(n.EventType))
                .ToArray();
        }

        return remaining;
    }

    /// <summary>
    /// Drops from each notification the recipients who have its actor on their
    /// personal blacklist.
    /// </summary>
    /// <remarks>
    /// Unconditional, with no settings flag in front of it. The flags of
    /// <see cref="UserBlacklistSettings"/> govern what a listing shows — comments,
    /// chat messages, games, blogs — which is a different question from whether
    /// the site writes to you about somebody. Blocking a person is the opt-in
    /// already given, and a notification is the one surface a reader cannot
    /// scroll past: it arrives in the list, by mail and through a bot.
    ///
    /// One statement per distinct actor, not per recipient. A batch out of the
    /// dispatcher is normally one event by one person addressed to many.
    /// </remarks>
    private async Task<IReadOnlyList<CreateNotification>> ExcludeBlockedRecipients(
        IReadOnlyList<CreateNotification> notifications, CancellationToken ct)
    {
        var byActor = notifications
            .Where(n => n.ActorId.HasValue && n.UsersInterested.Any())
            .GroupBy(n => n.ActorId!.Value)
            .ToArray();
        if (byActor.Length == 0)
        {
            return notifications;
        }

        var blocking = new Dictionary<Guid, IReadOnlySet<Guid>>();
        foreach (var group in byActor)
        {
            var audience = group.SelectMany(n => n.UsersInterested).Distinct().ToArray();
            blocking[group.Key] = await _blacklistChecker.GetOwnersBlockingAsync(group.Key, audience, ct);
        }

        return notifications
            .Select(n =>
            {
                if (!n.ActorId.HasValue ||
                    !blocking.TryGetValue(n.ActorId.Value, out var blockedBy) ||
                    blockedBy.Count == 0)
                {
                    return n;
                }

                var remaining = n.UsersInterested.Where(id => !blockedBy.Contains(id)).ToArray();
                return remaining.Length == n.UsersInterested.Count()
                    ? n
                    : n with { UsersInterested = remaining };
            })
            .ToArray();
    }

    #endregion

    #region Flushing

    /// <inheritdoc />
    public Task MarkAsReadAsync(Guid notificationId) =>
        _repository.MarkAsRead(notificationId, _identityProvider.Current.User.UserId);

    /// <inheritdoc />
    public Task MarkAllAsReadAsync() =>
        _repository.MarkAsRead(_identityProvider.Current.User.UserId);

    #endregion
}
