using System;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Game.Invitations;

/// <summary>
/// Game invitation DTO
/// </summary>
public class GameInvitation
{
    /// <summary>
    /// Invitation ID (token ID)
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Game ID
    /// </summary>
    public Guid GameId { get; set; }

    /// <summary>
    /// Game title
    /// </summary>
    public string GameTitle { get; set; } = string.Empty;

    /// <summary>
    /// Invited user (lightweight reference)
    /// </summary>
    public UserRef InvitedUser { get; set; } = null!;

    /// <summary>
    /// Username of user who sent the invitation (master)
    /// </summary>
    public string InviterUsername { get; set; } = string.Empty;

    /// <summary>
    /// Invitation type: player, reader, assistant
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// When the invitation was created
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// When the invitation expires
    /// </summary>
    public DateTimeOffset? ExpiresUtc { get; set; }
}
