using System;
using System.Collections.Generic;
using System.Linq;
using DM.Services.Community.BusinessProcesses.Subscriptions;
using DM.Services.Core.Dto.Enums;
using DM.Services.Core.Extensions;
using DM.Services.DataAccess;
using DM.Services.Notifications.Dto;
using Microsoft.EntityFrameworkCore;

namespace DM.Services.Notifications.Consumer.Implementation.Notifiers.Subscriptions;

/// <summary>
/// Generates notifications when a subscribed author creates a new game
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
                MasterLogin = g.Master!.Login,
                g.Status
            })
            .FirstOrDefaultAsync();

        if (gameData == null)
        {
            yield break;
        }

        // Only notify about games that are active (recruiting or playing)
        if (gameData.Status != ModuleStatus.Active)
        {
            yield break;
        }

        // Find all users subscribed to this author
        var subscriptions = await _subscriptionRepository.GetByTargetWithSettings(
            SubscriptionTargetType.Author,
            gameData.MasterId,
            SubscriptionSettings.AuthorNewContent);

        var subscriberIds = subscriptions
            .Select(s => s.SubscriberId)
            .ToArray();

        if (subscriberIds.Length == 0)
        {
            yield break;
        }

        yield return new CreateNotification
        {
            EventType = EventType.NewGameFromSubscribedAuthor,
            UsersInterested = subscriberIds,
            Metadata = new
            {
                GameId = gameData.GameId.EncodeToReadable(gameData.Title),
                GameTitle = gameData.Title,
                AuthorLogin = gameData.MasterLogin
            }
        };
    }
}
