using System;
using System.Collections.Generic;
using System.Linq;
using DM.Domain.Core.Subscriptions;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Extensions;
using DM.Infrastructure.Persistence;
using DM.Domain.Personal.Features.Notifications;
using Microsoft.EntityFrameworkCore;

namespace DM.Workers.NotificationDispatcher.Notifiers.Subscriptions;

/// <summary>
/// Generates "new game from subscribed author" notifications at game-creation
/// time — but only for games whose draft visibility is <see cref="DraftVisibility.Public"/>.
/// Private drafts stay invisible to non-team users; they get the notification
/// later, when the game transitions to Active (see
/// <c>GameActivatedNotificationGenerator</c>).
/// </summary>
internal class NewGameFromSubscribedAuthorNotificationGenerator : BaseNotificationGenerator
{
    private readonly DmDbContext _dbContext;
    private readonly ISubscriptionRepository _subscriptionRepository;

    /// <inheritdoc />
    public NewGameFromSubscribedAuthorNotificationGenerator(
        DmDbContext dbContext,
        ISubscriptionRepository subscriptionRepository)
    {
        _dbContext = dbContext;
        _subscriptionRepository = subscriptionRepository;
    }

    /// <inheritdoc />
    protected override EventType EventType => EventType.NewGame;

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
                g.DraftVisibility,
                AssistantIds = g.Assistants.Select(a => a.UserId).ToList()
            })
            .FirstOrDefaultAsync();

        if (gameData == null || gameData.DraftVisibility != DraftVisibility.Public)
        {
            yield break;
        }

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
