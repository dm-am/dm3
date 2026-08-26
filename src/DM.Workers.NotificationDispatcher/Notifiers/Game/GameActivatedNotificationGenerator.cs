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
/// Notification generator when a game becomes active.
/// Notifies: Subscribers of Master, Subscribers of Assistants
/// </summary>
internal class GameActivatedNotificationGenerator : BaseNotificationGenerator
{
    private readonly DmDbContext _dbContext;
    private readonly ISubscriptionRepository _subscriptionRepository;

    /// <inheritdoc />
    public GameActivatedNotificationGenerator(
        DmDbContext dbContext,
        ISubscriptionRepository subscriptionRepository)
    {
        _dbContext = dbContext;
        _subscriptionRepository = subscriptionRepository;
    }

    /// <inheritdoc />
    protected override EventType EventType => EventType.StatusGameActive;

    /// <inheritdoc />
    public override async IAsyncEnumerable<CreateNotification> Generate(Guid entityId)
    {
        var gameData = await _dbContext.Games
            .Where(g => g.GameId == entityId)
            .Select(g => new
            {
                g.GameId,
                g.Title,
                g.MasterId,
                MasterUsername = g.Master!.Username,
                AssistantIds = g.Assistants.Select(a => a.UserId).ToList()
            })
            .FirstOrDefaultAsync();

        if (gameData == null)
        {
            yield break;
        }

        // Readers of this game are deliberately absent. They are subscribed to the
        // game itself, so GameStatusChangedNotificationGenerator answers the same
        // event and already tells them it went active under the event type of the
        // activation. This notification renames the event to
        // NewGameFromSubscribedAuthor, which is only true for the audience that
        // could not see the game while it was a draft; delivered to a reader it was
        // a second copy of one activation calling a game they already follow new.
        var usersInterested = await SubscribedAudience.OfTeamAsync(
            _subscriptionRepository,
            gameData.MasterId,
            gameData.AssistantIds,
            SubscriptionSettings.AuthorGameEvents);

        if (usersInterested.Count == 0)
        {
            yield break;
        }

        yield return new CreateNotification
        {
            EventType = EventType.NewGameFromSubscribedAuthor,
            UsersInterested = usersInterested.ToArray(),
            ActorId = gameData.MasterId,
            Metadata = new
            {
                GameId = gameData.GameId.EncodeToReadable(gameData.Title),
                GameTitle = gameData.Title,
                AuthorUsername = gameData.MasterUsername
            }
        };
    }
}
