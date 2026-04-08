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
/// Notification generator for assistant invitations
/// </summary>
internal class GameAssistantInvitationNotificationGenerator : BaseNotificationGenerator
{
    private readonly DmDbContext _dbContext;

    /// <inheritdoc />
    public GameAssistantInvitationNotificationGenerator(DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    protected override EventType EventType => EventType.AssignmentRequestCreated;

    /// <inheritdoc />
    public override async IAsyncEnumerable<CreateNotification> Generate(Guid entityId)
    {
        var data = await _dbContext.Tokens
            .Where(t => t.TokenId == entityId && t.Type == TokenType.GameAssistantInvitation)
            .Select(t => new
            {
                t.UserId,
                t.EntityId,
                GameTitle = t.Game!.Title,
                InviterUsername = t.Game.Master!.Username
            })
            .FirstOrDefaultAsync();

        if (data == null || !data.EntityId.HasValue)
        {
            yield break;
        }

        yield return new CreateNotification
        {
            UsersInterested = new[] { data.UserId },
            Metadata = new
            {
                GameTitle = data.GameTitle,
                GameId = data.EntityId.Value.EncodeToReadable(),
                InviterUsername = data.InviterUsername,
                TokenId = entityId.EncodeToReadable(),
                InvitationType = "assistant"
            }
        };
    }
}
