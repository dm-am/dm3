using System;
using System.Collections.Generic;
using System.Linq;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Extensions;
using DM.Infrastructure.Persistence;
using DM.Domain.Personal.Features.Notifications;
using Microsoft.EntityFrameworkCore;

namespace DM.Workers.NotificationDispatcher.Implementation.Notifiers.Game;

/// <summary>
/// Notification generator for game closure warning.
/// Sent when a game will be closed due to inactivity.
/// Notifies: Master, Assistants
/// </summary>
internal class GameClosureWarningNotificationGenerator : BaseNotificationGenerator
{
    private readonly DmDbContext _dbContext;

    /// <inheritdoc />
    public GameClosureWarningNotificationGenerator(DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    protected override EventType EventType => EventType.GameClosureWarning;

    /// <inheritdoc />
    public override async IAsyncEnumerable<CreateNotification> Generate(Guid entityId)
    {
        var data = await _dbContext.Games
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

        if (data == null)
        {
            yield break;
        }

        // Notify Master and Assistants
        var usersInterested = new HashSet<Guid> { data.AuthorId };
        usersInterested.UnionWith(data.AssistantIds);

        yield return new CreateNotification
        {
            UsersInterested = usersInterested.ToArray(),
            Metadata = new
            {
                GameId = data.GameId.EncodeToReadable(data.Title),
                GameTitle = data.Title,
                MasterUsername = data.MasterUsername
            }
        };
    }
}
