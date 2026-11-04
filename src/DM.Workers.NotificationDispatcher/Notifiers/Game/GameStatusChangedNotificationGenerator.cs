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
/// Notification generator for game status changes.
/// Notifies: Master, Assistants, Players (always), Readers (if StatusChanges enabled)
/// </summary>
internal class GameStatusChangedNotificationGenerator : INotificationGenerator
{
    private static readonly EventType[] SupportedTypes =
    {
        EventType.StatusGameActive,
        EventType.StatusGameClosed,
        EventType.StatusGameFrozen,
        EventType.StatusGameFinished
    };

    private readonly DmDbContext _dbContext;
    private readonly ISubscriptionRepository _subscriptionRepository;

    public GameStatusChangedNotificationGenerator(
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
        var gameData = await _dbContext.Games
            .Where(g => g.GameId == entityId)
            .Select(g => new
            {
                g.GameId,
                g.Title,
                g.Status,
                g.MasterId,
                AssistantIds = g.Assistants.Select(a => a.UserId).ToList(),
                MasterUsername = g.Master!.Username,
                PlayerIds = g.Characters
                    .Where(c => !c.IsRemoved && c.Status == CharacterStatus.Active)
                    .Select(c => c.AuthorId!.Value)
                    .Distinct()
                    .ToList()
            })
            .FirstOrDefaultAsync();

        if (gameData == null)
        {
            yield break;
        }

        var readerSubscriptions = await _subscriptionRepository.GetByTargetWithSettingsAsync(
            SubscriptionTargetType.Game,
            entityId,
            SubscriptionSettings.StatusChanges);
        var readerIds = readerSubscriptions.Select(s => s.SubscriberId);

        var usersInterested = new HashSet<Guid> { gameData.MasterId };
        usersInterested.UnionWith(gameData.AssistantIds);
        usersInterested.UnionWith(gameData.PlayerIds);
        usersInterested.UnionWith(readerIds);

        // No ActorId: the game does not record who moved it, and the schedule moves it too.
        yield return new CreateNotification
        {
            UsersInterested = usersInterested,
            Metadata = new
            {
                GameTitle = gameData.Title,
                GameId = gameData.GameId.EncodeToReadable(),
                NewStatus = gameData.Status.ToString(),
                gameData.MasterUsername
            }
        };
    }
}
