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
/// Notification generator when a game becomes active.
/// Notifies: Subscribers of Master, Subscribers of Assistants, Readers
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
                g.AuthorId,
                MasterUsername = g.Author!.Username,
                AssistantIds = g.Assistants.Select(a => a.UserId).ToList()
            })
            .FirstOrDefaultAsync();

        if (gameData == null)
        {
            yield break;
        }

        var usersInterested = new HashSet<Guid>();

        // Get subscribers of the Master
        var masterSubscriptions = await _subscriptionRepository.GetByTargetWithSettingsAsync(
            SubscriptionTargetType.User,
            gameData.AuthorId,
            SubscriptionSettings.AuthorNewContent);
        usersInterested.UnionWith(masterSubscriptions.Select(s => s.SubscriberId));

        // Get subscribers of Assistants
        foreach (var assistantId in gameData.AssistantIds)
        {
            var assistantSubscriptions = await _subscriptionRepository.GetByTargetWithSettingsAsync(
                SubscriptionTargetType.User,
                assistantId,
                SubscriptionSettings.AuthorNewContent);
            usersInterested.UnionWith(assistantSubscriptions.Select(s => s.SubscriberId));
        }

        // Get game readers (subscribers to this specific game)
        var readerSubscriptions = await _subscriptionRepository.GetByTargetWithSettingsAsync(
            SubscriptionTargetType.Game,
            gameData.GameId,
            SubscriptionSettings.StatusChanges);
        usersInterested.UnionWith(readerSubscriptions.Select(s => s.SubscriberId));

        // Exclude game team members (they get notified via GameStatusChangedNotificationGenerator)
        usersInterested.Remove(gameData.AuthorId);
        foreach (var assistantId in gameData.AssistantIds)
        {
            usersInterested.Remove(assistantId);
        }

        if (usersInterested.Count == 0)
        {
            yield break;
        }

        yield return new CreateNotification
        {
            EventType = EventType.NewGameFromSubscribedAuthor,
            UsersInterested = usersInterested.ToArray(),
            Metadata = new
            {
                GameId = gameData.GameId.EncodeToReadable(gameData.Title),
                GameTitle = gameData.Title,
                AuthorUsername = gameData.MasterUsername
            }
        };
    }
}
