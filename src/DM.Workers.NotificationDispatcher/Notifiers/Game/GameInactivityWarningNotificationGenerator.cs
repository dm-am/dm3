using System;
using System.Collections.Generic;
using System.Linq;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Extensions;
using DM.Infrastructure.Persistence;
using DM.Domain.Personal.Features.Notifications;
using Microsoft.EntityFrameworkCore;

namespace DM.Workers.NotificationDispatcher.Notifiers.Game;

/// <summary>
/// Notification generator for game inactivity warning.
/// Sent when a game will be frozen due to 1 month without posts.
/// Notifies: Master, Assistants, Active Players
/// </summary>
internal class GameInactivityWarningNotificationGenerator : BaseNotificationGenerator
{
    private readonly DmDbContext _dbContext;

    public GameInactivityWarningNotificationGenerator(DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    protected override EventType EventType => EventType.GameInactivityWarning;

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
                AssistantIds = g.Assistants.Select(a => a.UserId).ToList(),
                ActivePlayerIds = g.Characters
                    .Where(c => c.AuthorId != null)
                    .Where(c => c.Status == CharacterStatus.Active || c.Status == CharacterStatus.UnderReview)
                    .Select(c => c.AuthorId!.Value)
                    .Distinct()
                    .ToList()
            })
            .FirstOrDefaultAsync();

        if (gameData == null)
        {
            yield break;
        }

        var usersInterested = new HashSet<Guid> { gameData.MasterId };
        usersInterested.UnionWith(gameData.AssistantIds);
        usersInterested.UnionWith(gameData.ActivePlayerIds);

        // No ActorId: the warning comes from a schedule.
        yield return new CreateNotification
        {
            UsersInterested = usersInterested,
            Metadata = new
            {
                GameId = gameData.GameId.EncodeToReadable(gameData.Title),
                GameTitle = gameData.Title,
                gameData.MasterUsername
            }
        };
    }
}
