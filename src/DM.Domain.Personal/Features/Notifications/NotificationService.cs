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

    /// <inheritdoc />
    public Task<long> CountUnreadAsync() =>
        _repository.CountUnread(_identityProvider.Current.User.UserId);

    /// <inheritdoc />
    public async Task<(IEnumerable<ListedNotification> Notifications, PagingResult Paging)> GetAsync(PagingQuery query)
    {
        var userId = _identityProvider.Current.User.UserId;
        var totalCount = await _repository.Count(userId);
        var pagingData = new PagingData(query, _identityProvider.Current.Settings.PageSize, totalCount);
        var notifications = await _repository.GetNotifications(userId, pagingData);
        return (notifications, pagingData.Result);
    }

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

        // The one place a personal blacklist reaches notifications, rather than in
        // every generator, where the next one would forget it.
        var addressedTo = await ExcludeBlockedRecipients(requested, ct);

        var notifications = addressedTo
            .Select(n => new CreatedNotification(n, _factory.Create(n, createDate)))
            .ToArray();

        // Not stored: one without recipients (global chat goes through the hub) and a
        // realtime-only one. Both are returned, the caller broadcasts by the assigned id.
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

    /// <remarks>
    /// Such a pair would make the unique index refuse the whole batch on every
    /// retry, so the duplicate is dropped with a warning instead.
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

    /// <remarks>
    /// The bus delivers at least once, and every channel is built from the survivors,
    /// so a replay is sent by none. A notification without an EventId is not
    /// deduplicated, and a realtime-only one has no row to find: a replay only
    /// re-reads a counter.
    /// </remarks>
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

    /// <remarks>
    /// Unconditional: the flags of <see cref="UserBlacklistSettings"/> govern what a
    /// listing shows, and a notification cannot be scrolled past. One statement per
    /// distinct actor, not per recipient.
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

    /// <inheritdoc />
    public Task MarkAsReadAsync(Guid notificationId) =>
        _repository.MarkAsRead(notificationId, _identityProvider.Current.User.UserId);

    /// <inheritdoc />
    public Task MarkAllAsReadAsync() =>
        _repository.MarkAsRead(_identityProvider.Current.User.UserId);
}
