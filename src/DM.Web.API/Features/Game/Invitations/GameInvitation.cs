using System;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Game.Invitations;

/// <summary>
/// Game invitation DTO
/// </summary>
public class GameInvitation : InvitationRecord
{
    /// <summary>
    /// Game ID
    /// </summary>
    public Guid GameId { get; set; }

    /// <summary>
    /// Game title
    /// </summary>
    public string GameTitle { get; set; } = string.Empty;
}
