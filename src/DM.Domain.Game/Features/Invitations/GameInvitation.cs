using System;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;

namespace DM.Domain.Game.Features.Invitations;

public class GameInvitation
{
    public Guid TokenId { get; set; }

    public Guid GameId { get; set; }

    public string GameTitle { get; set; } = null!;

    public GeneralUser InvitedUser { get; set; } = null!;

    public GeneralUser InvitedBy { get; set; } = null!;

    public GameRole TargetRole { get; set; }

    public DateTimeOffset CreatedUtc { get; set; }

    public DateTimeOffset ExpiresUtc { get; set; }
}

public class GameInvitationToken
{
    public Guid TokenId { get; set; }

    public Guid UserId { get; set; }

    public TokenType TokenType { get; set; }

    /// <summary>
    /// The game
    /// </summary>
    public Guid? EntityId { get; set; }

    public DateTimeOffset CreatedUtc { get; set; }
}

public class CancelledInvitation
{
    public Guid TokenId { get; set; }

    public TokenType TokenType { get; set; }
}
