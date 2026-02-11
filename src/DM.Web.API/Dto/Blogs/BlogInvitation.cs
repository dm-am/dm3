using System;
using System.ComponentModel.DataAnnotations;

namespace DM.Web.API.Dto.Blogs;

/// <summary>
/// Blog invitation details
/// </summary>
public class BlogInvitation
{
    /// <summary>
    /// Invitation token ID
    /// </summary>
    public Guid TokenId { get; set; }

    /// <summary>
    /// Blog ID the invitation is for
    /// </summary>
    public Guid BlogId { get; set; }

    /// <summary>
    /// Blog title
    /// </summary>
    public string BlogTitle { get; set; } = string.Empty;

    /// <summary>
    /// Invited user's login
    /// </summary>
    public string InvitedUserLogin { get; set; } = string.Empty;

    /// <summary>
    /// Role being offered
    /// </summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// When the invitation was created
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// When the invitation expires
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; set; }
}

/// <summary>
/// Request to create an assistant invitation
/// </summary>
public class CreateAssistantInvitationRequest
{
    /// <summary>
    /// Login of user to invite
    /// </summary>
    [Required]
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
    [Required]
    public string Login { get; set; } = string.Empty;
}
