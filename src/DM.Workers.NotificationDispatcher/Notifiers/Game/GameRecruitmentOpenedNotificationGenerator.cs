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
/// Notification generator when game recruitment is opened.
/// Notifies: Subscribers of Master, Subscribers of Assistants, Readers
/// </summary>
internal class GameRecruitmentOpenedNotificationGenerator : BaseNotificationGenerator
{
    private readonly DmDbContext _dbContext;
    private readonly ISubscriptionRepository _subscriptionRepository;

    public GameRecruitmentOpenedNotificationGenerator(
        DmDbContext dbContext,
        ISubscriptionRepository subscriptionRepository)
    {
        _dbContext = dbContext;
        _subscriptionRepository = subscriptionRepository;
    }

    /// <inheritdoc />
    protected override EventType EventType => EventType.GameRecruitmentOpened;

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

        var usersInterested = new HashSet<Guid>();

        var masterSubscriptions = await _subscriptionRepository.GetByTargetWithSettingsAsync(
            SubscriptionTargetType.User,
            gameData.MasterId,
            SubscriptionSettings.AuthorGameEvents);
        usersInterested.UnionWith(masterSubscriptions.Select(s => s.SubscriberId));

        foreach (var assistantId in gameData.AssistantIds)
        {
            var assistantSubscriptions = await _subscriptionRepository.GetByTargetWithSettingsAsync(
                SubscriptionTargetType.User,
                assistantId,
                SubscriptionSettings.AuthorGameEvents);
            usersInterested.UnionWith(assistantSubscriptions.Select(s => s.SubscriberId));
        }

        var readerSubscriptions = await _subscriptionRepository.GetByTargetWithSettingsAsync(
            SubscriptionTargetType.Game,
            gameData.GameId,
            SubscriptionSettings.StatusChanges);
        usersInterested.UnionWith(readerSubscriptions.Select(s => s.SubscriberId));

        usersInterested.Remove(gameData.MasterId);
        foreach (var assistantId in gameData.AssistantIds)
        {
            usersInterested.Remove(assistantId);
        }

        if (usersInterested.Count == 0)
        {
            yield break;
        }

        // The master is the actor: the game does not record which team member opened recruitment.
        yield return new CreateNotification
        {
            UsersInterested = usersInterested.ToArray(),
            ActorId = gameData.MasterId,
            Metadata = new
            {
                GameId = gameData.GameId.EncodeToReadable(gameData.Title),
                GameTitle = gameData.Title,
                MasterUsername = gameData.MasterUsername
            }
        };
    }
}
