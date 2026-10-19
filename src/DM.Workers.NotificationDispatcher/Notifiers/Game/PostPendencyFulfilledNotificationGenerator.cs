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
/// Notification generator when a post pendency is fulfilled.
/// Notifies: The user who created the pendency (CreatedBy)
/// </summary>
internal class PostPendencyFulfilledNotificationGenerator : BaseNotificationGenerator
{
    private readonly DmDbContext _dbContext;

    public PostPendencyFulfilledNotificationGenerator(DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    protected override EventType EventType => EventType.RoomPendencyFulfilled;

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
                CharacterName = p.Character.Name,
                p.WaitingForUserId,
                CharacterAuthorId = p.Character.AuthorId,
                CharacterOwnerUsername = p.Character.Author!.Username,
                p.CreatedById
            })
            .FirstOrDefaultAsync();

        if (pendencyData == null)
        {
            yield break;
        }

        var fulfilledByUsername = pendencyData.CharacterOwnerUsername;
        var fulfilledByUserId = pendencyData.WaitingForUserId ?? pendencyData.CharacterAuthorId!.Value;

        // The creator of the pendency, unless they fulfilled it themselves
        if (pendencyData.CreatedById == fulfilledByUserId)
        {
            yield break;
        }

        yield return new CreateNotification
        {
            UsersInterested = [pendencyData.CreatedById],
            ActorId = fulfilledByUserId,
            Metadata = new
            {
                PendencyId = pendencyData.PendencyId.EncodeToReadable(),
                GameId = pendencyData.GameId.EncodeToReadable(pendencyData.GameTitle),
                pendencyData.GameTitle,
                RoomId = pendencyData.RoomId.EncodeToReadable(),
                pendencyData.RoomTitle,
                pendencyData.CharacterName,
                FulfilledByUsername = fulfilledByUsername
            }
        };
    }
}
