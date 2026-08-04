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
/// Notification generator for new PC character creation.
/// Notifies: Master, Assistants (always), Readers (if CharacterUpdates enabled)
/// </summary>
internal class GameCharacterCreatedNotificationGenerator : BaseNotificationGenerator
{
    private readonly DmDbContext _dbContext;
    private readonly ISubscriptionRepository _subscriptionRepository;

    /// <inheritdoc />
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
        var data = await _dbContext.Characters
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

        if (data == null)
        {
            yield break;
        }

        // Direct game members: Master and Assistants (always notified)
        var usersInterested = new HashSet<Guid> { data.GameMasterId };
        usersInterested.UnionWith(data.AssistantIds);

        // Subscribers with CharacterUpdates enabled
        var readerSubscriptions = await _subscriptionRepository.GetByTargetWithSettingsAsync(
            SubscriptionTargetType.Game,
            data.GameId,
            SubscriptionSettings.CharacterUpdates);
        usersInterested.UnionWith(readerSubscriptions.Select(s => s.SubscriberId));

        // Don't notify the character author
        usersInterested.Remove(data.AuthorId!.Value);

        if (usersInterested.Count == 0)
        {
            yield break;
        }

        yield return new CreateNotification
        {
            UsersInterested = usersInterested,
            ActorId = data.AuthorId,
            Metadata = new
            {
                AuthorUsername = data.Username,
                GameTitle = data.Title,
                GameId = data.GameId.EncodeToReadable()
            }
        };
    }
}
