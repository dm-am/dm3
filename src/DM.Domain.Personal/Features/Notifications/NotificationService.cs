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

namespace DM.Domain.Personal.Features.Notifications;

/// <inheritdoc />
internal class NotificationService : INotificationService
{
    private readonly IIdentityProvider _identityProvider;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly INotificationFactory _factory;
    private readonly INotificationRepository _repository;
    private readonly IUserBlacklistChecker _blacklistChecker;

    /// <inheritdoc />
    public NotificationService(
        IIdentityProvider identityProvider,
        IDateTimeProvider dateTimeProvider,
        INotificationFactory factory,
        INotificationRepository repository,
        IUserBlacklistChecker blacklistChecker)
    {
        _identityProvider = identityProvider;
        _dateTimeProvider = dateTimeProvider;
        _factory = factory;
        _repository = repository;
        _blacklistChecker = blacklistChecker;
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
        var requested = createNotifications.ToArray();

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
