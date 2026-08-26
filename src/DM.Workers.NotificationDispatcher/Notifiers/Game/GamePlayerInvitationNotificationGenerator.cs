using System;
using System.Collections.Generic;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Extensions;
using DM.Infrastructure.Persistence;
using DM.Infrastructure.Persistence.Entities.Account;
using DM.Domain.Personal.Features.Notifications;

namespace DM.Workers.NotificationDispatcher.Notifiers.Game;

/// <summary>
/// Notification generator for player invitations
/// </summary>
internal class GamePlayerInvitationNotificationGenerator : GameInvitationNotificationGenerator
{
    /// <inheritdoc />
    public GamePlayerInvitationNotificationGenerator(DmDbContext dbContext)
        : base(dbContext)
    {
    }

    /// <inheritdoc />
    protected override EventType EventType => EventType.PlayerInvitationCreated;

    /// <inheritdoc />
    protected override TokenType TokenType => TokenType.GamePlayerInvitation;

    /// <inheritdoc />
    public override async IAsyncEnumerable<CreateNotification> Generate(Guid entityId)
    {
        var invitation = await Subject(entityId);
        if (invitation == null)
        {
            yield break;
        }

        yield return new CreateNotification
        {
            UsersInterested = new[] { invitation.UserId },
            ActorId = invitation.CreatorId ?? invitation.MasterId,
            Metadata = new
            {
                GameTitle = invitation.GameTitle,
                GameId = invitation.GameId.EncodeToReadable(),
                InviterUsername = invitation.InviterUsername,
                TokenId = entityId.EncodeToReadable(),
                InvitationType = "player"
            }
        };
    }
}
