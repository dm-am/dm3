using System;
using DM.Domain.Core.Enums;

namespace DM.Domain.Game.Features.Invitations;

public class CreateGameInvitationEntity
{
    /// <summary>
    /// Pre-generated
    /// </summary>
    public Guid TokenId { get; set; }

    public Guid GameId { get; set; }

    /// <summary>
    /// The invitee
    /// </summary>
    public Guid UserId { get; set; }

    public Guid CreatorId { get; set; }

    public TokenType TokenType { get; set; }

    public DateTimeOffset CreatedUtc { get; set; }
}
