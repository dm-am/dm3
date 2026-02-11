using System;
using System.Collections.Generic;
using System.Linq;
using DM.Services.Core.Dto.Enums;
using DM.Services.Core.Extensions;
using DM.Services.DataAccess;
using DM.Services.DataAccess.BusinessObjects.Users;
using DM.Services.Notifications.Dto;
using Microsoft.EntityFrameworkCore;

namespace DM.Services.Notifications.Consumer.Implementation.Notifiers.Game;

/// <summary>
/// Notification generator for reader invitations
/// </summary>
internal class ReaderInvitationNotificationGenerator : BaseNotificationGenerator
{
    private readonly DmDbContext _dbContext;

    /// <inheritdoc />
    public ReaderInvitationNotificationGenerator(DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    protected override EventType EventType => EventType.ReaderInvitationCreated;

    /// <inheritdoc />
    public override async IAsyncEnumerable<CreateNotification> Generate(Guid entityId)
    {
        var data = await _dbContext.Tokens
            .Where(t => t.TokenId == entityId && t.Type == TokenType.ReaderInvitation)
            .Select(t => new
            {
                t.UserId,
                t.EntityId,
                GameTitle = t.Game!.Title,
                InviterLogin = t.Game.Master!.Login
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
                InviterLogin = data.InviterLogin,
                TokenId = entityId.EncodeToReadable(),
                InvitationType = "reader"
            }
        };
    }
}
