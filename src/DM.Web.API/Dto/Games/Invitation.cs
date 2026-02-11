using System;
using DM.Web.API.Dto.Users;

namespace DM.Web.API.Dto.Games;

/// <summary>
/// Game invitation DTO
/// </summary>
public class Invitation
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
    public string GameTitle { get; set; } = null!;

    /// <summary>
    /// Invited user
    /// </summary>
    public User InvitedUser { get; set; } = null!;

    /// <summary>
    /// User who sent the invitation (master)
    /// </summary>
    public string InviterLogin { get; set; } = null!;

    /// <summary>
    /// Invitation type
    /// </summary>
    public string Type { get; set; } = null!;

    /// <summary>
    /// Creation date
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }
}

/// <summary>
/// Request to create an invitation
/// </summary>
public class CreateInvitation
{
    /// <summary>
    /// User login to invite
    /// </summary>
    public string Login { get; set; } = null!;
}
