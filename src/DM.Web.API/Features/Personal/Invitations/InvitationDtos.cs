using System;

namespace DM.Web.API.Features.Personal.Invitations;

/// <summary>
/// Received invitation DTO (for accept/reject by user)
/// </summary>
/// <remarks>
/// Used in Personal API to show all pending invitations (games and blogs combined).
/// </remarks>
public class ReceivedInvitation
{
    /// <summary>
    /// Invitation ID (token ID)
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Entity type: "game" or "blog"
    /// </summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>
    /// Entity ID (game or blog)
    /// </summary>
    public Guid EntityId { get; set; }

    /// <summary>
    /// Entity title (game or blog title)
    /// </summary>
    public string EntityTitle { get; set; } = string.Empty;

    /// <summary>
    /// Username of user who sent the invitation
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
