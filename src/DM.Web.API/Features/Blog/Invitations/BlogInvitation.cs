using System;
using DM.Web.API.Features.Community.Users;

namespace DM.Web.API.Features.Blog.Invitations;

/// <summary>
/// Blog invitation DTO
/// </summary>
public class BlogInvitation
{
    /// <summary>
    /// Invitation ID (token ID)
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Blog ID
    /// </summary>
    public Guid BlogId { get; set; }

    /// <summary>
    /// Blog title
    /// </summary>
    public string BlogTitle { get; set; } = string.Empty;

    /// <summary>
    /// Invited user
    /// </summary>
    public User InvitedUser { get; set; } = null!;

    /// <summary>
    /// Username of user who sent the invitation (BlogModel owner)
    /// </summary>
    public string InviterUsername { get; set; } = string.Empty;

    /// <summary>
    /// Invitation type: assistant, reader
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

/// <summary>
/// Request to create an invitation
/// </summary>
public class CreateInvitationRequest
{
    /// <summary>
    /// Login of user to invite
    /// </summary>
    [System.ComponentModel.DataAnnotations.Required]
    public string Username { get; set; } = string.Empty;
}

/// <summary>
/// Request to create an assistant invitation
/// </summary>
public class CreateAssistantInvitationRequest
{
    /// <summary>
    /// Login of user to invite
    /// </summary>
    [System.ComponentModel.DataAnnotations.Required]
    public string Login { get; set; } = string.Empty;
}

/// <summary>
/// Request to create a reader invitation
/// </summary>
public class CreateReaderInvitationRequest
{
    /// <summary>
    /// Login of user to invite
    /// </summary>
    [System.ComponentModel.DataAnnotations.Required]
    public string Login { get; set; } = string.Empty;
}
