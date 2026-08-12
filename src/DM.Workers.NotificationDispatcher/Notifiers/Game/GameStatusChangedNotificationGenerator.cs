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

    /// <inheritdoc />
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
        var data = await _dbContext.Games
            .Where(g => g.GameId == entityId)
            .Select(g => new
            {
                g.GameId,
                g.Title,
                g.Status,
                g.MasterId,
                AssistantIds = g.Assistants.Select(a => a.UserId).ToList(),
                MasterUsername = g.Master!.Username,
                // Get all active players (users who own active characters)
                PlayerIds = g.Characters
                    .Where(c => !c.IsRemoved && c.Status == CharacterStatus.Active)
                    .Select(c => c.AuthorId!.Value)
                    .Distinct()
                    .ToList()
            })
            .FirstOrDefaultAsync();

        if (data == null)
        {
            yield break;
        }

        // Get subscribers (readers) who have StatusChanges enabled
        var readerSubscriptions = await _subscriptionRepository.GetByTargetWithSettingsAsync(
            SubscriptionTargetType.Game,
            entityId,
            SubscriptionSettings.StatusChanges);
        var readerIds = readerSubscriptions.Select(s => s.SubscriberId);

        // Combine all recipients: Master, Assistants, Players (always), Readers (with StatusChanges)
        var usersInterested = new HashSet<Guid> { data.MasterId };
        usersInterested.UnionWith(data.AssistantIds);
        usersInterested.UnionWith(data.PlayerIds);
        usersInterested.UnionWith(readerIds);

        if (usersInterested.Count == 0)
        {
            yield break;
        }

        // ActorId stays null because the schema does not keep who moved the game:
        // a game stores its master, not the author of the transition. Two of the
        // four events here are raised by GameInactivityProcessor on a schedule
        // with no human at all, and a moderator may close somebody else's game,
        // so MasterUsername below names the owner rather than the one who acted.
        yield return new CreateNotification
        {
            UsersInterested = usersInterested,
            Metadata = new
            {
                GameTitle = data.Title,
                GameId = data.GameId.EncodeToReadable(),
                NewStatus = data.Status.ToString(),
                MasterUsername = data.MasterUsername
            }
        };
    }
}
