using System;
using System.Collections.Generic;
using System.Linq;
using DM.Domain.Core.Subscriptions;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Extensions;
using DM.Infrastructure.Persistence;
using DM.Domain.Personal.Features.Notifications;
using Microsoft.EntityFrameworkCore;

namespace DM.Workers.NotificationDispatcher.Implementation.Notifiers.Game;

/// <summary>
/// Notification generator for PC character status changes.
/// Notifies: Character owner (always), Readers (if StatusChanges enabled, only for public statuses)
/// </summary>
internal class GameCharacterStatusChangedNotificationGenerator : INotificationGenerator
{
    private static readonly EventType[] SupportedTypes =
    {
        EventType.StatusCharacterDeclined,
        EventType.StatusCharacterAccepted,
        EventType.StatusCharacterDied,
        EventType.StatusCharacterResurrected,
        EventType.StatusCharacterLeft,
        EventType.StatusCharacterReturned,
        EventType.StatusCharacterExiled,
        EventType.StatusCharacterRetired
    };

    // Statuses that are public and can be shared with subscribers
    // Declined is private between master and player
    private static readonly CharacterStatus[] PrivateStatuses =
    {
        CharacterStatus.Declined
    };

    private readonly DmDbContext _dbContext;
    private readonly ISubscriptionRepository _subscriptionRepository;

    /// <inheritdoc />
    public GameCharacterStatusChangedNotificationGenerator(
        DmDbContext dbContext,
        ISubscriptionRepository subscriptionRepository)
    {
        _dbContext = dbContext;
        _subscriptionRepository = subscriptionRepository;
    }

    /// <inheritdoc />
    public bool CanResolve(EventType eventType) => SupportedTypes.Contains(eventType);

    /// <inheritdoc />
    public async IAsyncEnumerable<CreateNotification> Generate(Guid entityId)
    {
        var data = await _dbContext.Characters
            .Where(c => c.CharacterId == entityId && !c.IsNpc)
            .Select(c => new
            {
                c.CharacterId,
                c.Name,
                c.AuthorId,
                c.Status,
                c.GameId,
                GameTitle = c.Game!.Title,
                MasterUsername = c.Game.Author!.Username,
                GameAuthorId = c.Game.AuthorId,
                AssistantIds = c.Game.Assistants.Select(a => a.UserId).ToList()
            })
            .FirstOrDefaultAsync();

        if (data == null)
        {
            yield break;
        }

        var usersInterested = new HashSet<Guid>();

        // Always notify character owner (unless they are the master who made the change)
        if (data.AuthorId.HasValue && data.AuthorId != data.GameAuthorId)
        {
            usersInterested.Add(data.AuthorId.Value);
        }

        // For public statuses (not Declined), also notify game subscribers with StatusChanges enabled
        if (!PrivateStatuses.Contains(data.Status))
        {
            var readerSubscriptions = await _subscriptionRepository.GetByTargetWithSettingsAsync(
                SubscriptionTargetType.Game,
                data.GameId,
                SubscriptionSettings.StatusChanges);

            foreach (var sub in readerSubscriptions)
            {
                // Don't notify game team members (they see it in game)
                if (sub.SubscriberId != data.GameAuthorId &&
                    !data.AssistantIds.Contains(sub.SubscriberId))
                {
                    usersInterested.Add(sub.SubscriberId);
                }
            }
        }

        if (usersInterested.Count == 0)
        {
            yield break;
        }

        yield return new CreateNotification
        {
            UsersInterested = usersInterested,
            Metadata = new
            {
                CharacterName = data.Name,
                CharacterId = data.CharacterId.EncodeToReadable(),
                GameTitle = data.GameTitle,
                GameId = data.GameId.EncodeToReadable(),
                NewStatus = data.Status.ToString(),
                MasterUsername = data.MasterUsername
            }
        };
    }
}
