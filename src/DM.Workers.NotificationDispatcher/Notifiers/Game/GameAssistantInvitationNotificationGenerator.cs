using System;
using System.Collections.Generic;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Extensions;
using DM.Infrastructure.Persistence;
using DM.Infrastructure.Persistence.Entities.Account;
using DM.Domain.Personal.Features.Notifications;

namespace DM.Workers.NotificationDispatcher.Notifiers.Game;

/// <summary>
/// Notification generator for assistant invitations
/// </summary>
internal class GameAssistantInvitationNotificationGenerator : GameInvitationNotificationGenerator
{
    /// <inheritdoc />
    public GameAssistantInvitationNotificationGenerator(DmDbContext dbContext)
        : base(dbContext)
    {
    }

    /// <inheritdoc />
    protected override EventType EventType => EventType.AssignmentRequestCreated;

    /// <inheritdoc />
    protected override TokenType TokenType => TokenType.GameAssistantInvitation;

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
                InvitationType = "assistant"
            }
        };
    }
}
