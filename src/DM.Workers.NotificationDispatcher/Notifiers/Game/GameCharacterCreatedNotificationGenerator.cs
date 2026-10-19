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
/// Notification generator for new PC character creation.
/// Notifies: Master, Assistants (always), Readers (if CharacterUpdates enabled)
/// </summary>
internal class GameCharacterCreatedNotificationGenerator : BaseNotificationGenerator
{
    private readonly DmDbContext _dbContext;
    private readonly ISubscriptionRepository _subscriptionRepository;

    public GameCharacterCreatedNotificationGenerator(
        DmDbContext dbContext,
        ISubscriptionRepository subscriptionRepository)
    {
        _dbContext = dbContext;
        _subscriptionRepository = subscriptionRepository;
    }

    /// <inheritdoc />
    protected override EventType EventType => EventType.NewCharacter;

    /// <inheritdoc />
    public override async IAsyncEnumerable<CreateNotification> Generate(Guid entityId)
    {
        var characterData = await _dbContext.Characters
            .Where(c => c.CharacterId == entityId && !c.IsNpc)
            .Select(c => new
            {
                c.GameId,
                c.Game!.Title,
                c.Author!.Username,
                c.AuthorId,
                GameMasterId = c.Game.MasterId,
                AssistantIds = c.Game.Assistants.Select(a => a.UserId).ToList()
            })
            .FirstOrDefaultAsync();

        if (characterData == null)
        {
            yield break;
        }

        var usersInterested = new HashSet<Guid> { characterData.GameMasterId };
        usersInterested.UnionWith(characterData.AssistantIds);

        var readerSubscriptions = await _subscriptionRepository.GetByTargetWithSettingsAsync(
            SubscriptionTargetType.Game,
            characterData.GameId,
            SubscriptionSettings.CharacterUpdates);
        usersInterested.UnionWith(readerSubscriptions.Select(s => s.SubscriberId));

        usersInterested.Remove(characterData.AuthorId!.Value);

        if (usersInterested.Count == 0)
        {
            yield break;
        }

        yield return new CreateNotification
        {
            UsersInterested = usersInterested,
            ActorId = characterData.AuthorId,
            Metadata = new
            {
                AuthorUsername = characterData.Username,
                GameTitle = characterData.Title,
                GameId = characterData.GameId.EncodeToReadable()
            }
        };
    }
}
