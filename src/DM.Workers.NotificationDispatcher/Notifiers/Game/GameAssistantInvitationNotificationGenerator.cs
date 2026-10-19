using System;
using System.Collections.Generic;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Extensions;
using DM.Infrastructure.Persistence;
using DM.Domain.Personal.Features.Notifications;

namespace DM.Workers.NotificationDispatcher.Notifiers.Game;

internal class GameAssistantInvitationNotificationGenerator : GameInvitationNotificationGenerator
{
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
            UsersInterested = [invitation.UserId],
            ActorId = invitation.CreatorId ?? invitation.MasterId,
            Metadata = new
            {
                invitation.GameTitle,
                GameId = invitation.GameId.EncodeToReadable(),
                invitation.InviterUsername,
                TokenId = entityId.EncodeToReadable(),
                InvitationType = "assistant"
            }
        };
    }
}
