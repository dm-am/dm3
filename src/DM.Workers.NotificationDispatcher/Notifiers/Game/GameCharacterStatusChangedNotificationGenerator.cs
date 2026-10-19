using System;
using System.Collections.Generic;
using System.Linq;
using DM.Domain.Core.Subscriptions;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Extensions;
using DM.Infrastructure.Persistence;
using DM.Domain.Personal.Features.Notifications;
using Microsoft.EntityFrameworkCore;

namespace DM.Workers.NotificationDispatcher.Notifiers.Game;

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
        EventType.StatusCharacterReadmitted,
        EventType.StatusCharacterLeft,
        EventType.StatusCharacterExiled
    };


    private readonly DmDbContext _dbContext;
    private readonly ISubscriptionRepository _subscriptionRepository;

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
        var characterData = await _dbContext.Characters
            .Where(c => c.CharacterId == entityId && !c.IsNpc)
            .Select(c => new
            {
                c.CharacterId,
                c.Name,
                c.AuthorId,
                c.Status,
                c.GameId,
                GameTitle = c.Game!.Title,
                MasterUsername = c.Game.Master!.Username,
                GameMasterId = c.Game.MasterId,
                AssistantIds = c.Game.Assistants.Select(a => a.UserId).ToList()
            })
            .FirstOrDefaultAsync();

        if (characterData == null)
        {
            yield break;
        }

        var usersInterested = new HashSet<Guid>();

        // The owner, unless they are the master who made the change
        if (characterData.AuthorId.HasValue && characterData.AuthorId != characterData.GameMasterId)
        {
            usersInterested.Add(characterData.AuthorId.Value);
        }

        // Declined is private between master and player
        if (characterData.Status != CharacterStatus.Declined)
        {
            var readerSubscriptions = await _subscriptionRepository.GetByTargetWithSettingsAsync(
                SubscriptionTargetType.Game,
                characterData.GameId,
                SubscriptionSettings.StatusChanges);

            foreach (var sub in readerSubscriptions)
            {
                // The team sees it in the game
                if (sub.SubscriberId != characterData.GameMasterId &&
                    !characterData.AssistantIds.Contains(sub.SubscriberId))
                {
                    usersInterested.Add(sub.SubscriberId);
                }
            }
        }

        if (usersInterested.Count == 0)
        {
            yield break;
        }

        // No ActorId: characters do not record who moved the status.
        yield return new CreateNotification
        {
            UsersInterested = usersInterested,
            Metadata = new
            {
                CharacterName = characterData.Name,
                CharacterId = characterData.CharacterId.EncodeToReadable(),
                characterData.GameTitle,
                GameId = characterData.GameId.EncodeToReadable(),
                NewStatus = characterData.Status.ToString(),
                characterData.MasterUsername
            }
        };
    }
}
