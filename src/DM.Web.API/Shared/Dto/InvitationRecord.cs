using System;

namespace DM.Web.API.Shared.Dto;

/// <summary>
/// What an invitation to a game and to a blog both carry
/// </summary>
public abstract class InvitationRecord
{
    /// <summary>
    /// Invitation ID (token ID)
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Invited user (lightweight reference)
    /// </summary>
    public UserRef InvitedUser { get; set; } = null!;

    /// <summary>
    /// Username of user who sent the invitation (the master of the game, the owner of the blog)
    /// </summary>
    public string InviterUsername { get; set; } = string.Empty;

    /// <summary>
    /// Invitation type: player, reader, assistant to a game; assistant, reader to a blog
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
