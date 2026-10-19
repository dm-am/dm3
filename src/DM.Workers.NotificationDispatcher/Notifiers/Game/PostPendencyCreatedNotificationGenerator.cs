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
/// Notification generator when a post pendency is created.
/// Notifies: The user who is expected to post (WaitingForUser)
/// </summary>
internal class PostPendencyCreatedNotificationGenerator : BaseNotificationGenerator
{
    private readonly DmDbContext _dbContext;

    public PostPendencyCreatedNotificationGenerator(DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    protected override EventType EventType => EventType.RoomPendencyCreated;

    /// <inheritdoc />
    public override async IAsyncEnumerable<CreateNotification> Generate(Guid entityId)
    {
        var pendencyData = await _dbContext.PostPendencies
            .Where(p => p.PendencyId == entityId)
            .Select(p => new
            {
                p.PendencyId,
                p.RoomId,
                RoomTitle = p.Room.Title,
                p.Room.GameId,
                GameTitle = p.Room.Game!.Title,
                p.CharacterId,
                CharacterName = p.Character.Name,
                p.WaitingForUserId,
                CharacterAuthorId = p.Character.AuthorId,
                p.CreatedById,
                CreatedByUsername = p.CreatedBy.Username
            })
            .FirstOrDefaultAsync();

        if (pendencyData == null)
        {
            yield break;
        }

        // WaitingForUser if set, otherwise the character owner
        var recipientId = pendencyData.WaitingForUserId ?? pendencyData.CharacterAuthorId!.Value;

        if (recipientId == pendencyData.CreatedById)
        {
            yield break;
        }

        yield return new CreateNotification
        {
            UsersInterested = [recipientId],
            ActorId = pendencyData.CreatedById,
            Metadata = new
            {
                PendencyId = pendencyData.PendencyId.EncodeToReadable(),
                GameId = pendencyData.GameId.EncodeToReadable(pendencyData.GameTitle),
                pendencyData.GameTitle,
                RoomId = pendencyData.RoomId.EncodeToReadable(),
                pendencyData.RoomTitle,
                pendencyData.CharacterName,
                pendencyData.CreatedByUsername
            }
        };
    }
}
