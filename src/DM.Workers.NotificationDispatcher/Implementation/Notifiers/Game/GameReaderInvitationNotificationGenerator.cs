using System;
using System.Collections.Generic;
using System.Linq;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Extensions;
using DM.Infrastructure.Persistence;
using DM.Infrastructure.Persistence.Entities.Account;
using DM.Domain.Personal.Features.Notifications;
using Microsoft.EntityFrameworkCore;

namespace DM.Workers.NotificationDispatcher.Implementation.Notifiers.Game;

/// <summary>
/// Notification generator for reader invitations
/// </summary>
internal class GameReaderInvitationNotificationGenerator : BaseNotificationGenerator
{
    private readonly DmDbContext _dbContext;

    /// <inheritdoc />
    public GameReaderInvitationNotificationGenerator(DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    protected override EventType EventType => EventType.ReaderInvitationCreated;

    /// <inheritdoc />
    public override async IAsyncEnumerable<CreateNotification> Generate(Guid entityId)
    {
        var data = await _dbContext.Tokens
            .Where(t => t.TokenId == entityId && t.Type == TokenType.GameReaderInvitation)
            .Select(t => new
            {
                t.UserId,
                t.EntityId,
                t.CreatorId,
                GameTitle = t.Game!.Title,
                MasterId = t.Game.MasterId,
                InviterUsername = t.Game.Master!.Username
            })
            .FirstOrDefaultAsync();

        if (data == null || !data.EntityId.HasValue)
        {
            yield break;
        }

        // The inviter is the token's creator, master or assistant. Falling back to
        // the master keeps the filter on the person InviterUsername names when a
        // token carries no creator, the way BlogInvitationRepository resolves its
        // inviter.
        yield return new CreateNotification
        {
            UsersInterested = new[] { data.UserId },
            ActorId = data.CreatorId ?? data.MasterId,
            Metadata = new
            {
                GameTitle = data.GameTitle,
                GameId = data.EntityId.Value.EncodeToReadable(),
                InviterUsername = data.InviterUsername,
                TokenId = entityId.EncodeToReadable(),
                InvitationType = "reader"
            }
        };
    }
}
