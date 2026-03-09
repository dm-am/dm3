using System;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;

namespace DM.Domain.Blog.Features.Invitations;

/// <summary>
/// Pending blog invitation
/// </summary>
public class BlogInvitation
{
    /// <summary>
    /// Token ID
    /// </summary>
    public Guid TokenId { get; set; }

    /// <summary>
    /// Blog ID
    /// </summary>
    public Guid BlogId { get; set; }

    /// <summary>
    /// Blog title
    /// </summary>
    public string BlogTitle { get; set; } = null!;

    /// <summary>
    /// Invited user
    /// </summary>
    public GeneralUser InvitedUser { get; set; } = null!;

    /// <summary>
    /// User who sent the invitation
    /// </summary>
    public GeneralUser InvitedBy { get; set; } = null!;

    /// <summary>
    /// Target role (Reader or Assistant)
    /// </summary>
    public BlogRole TargetRole { get; set; }

    /// <summary>
    /// When the invitation was created
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// When the invitation expires
    /// </summary>
    public DateTimeOffset ExpiresUtc { get; set; }
}

/// <summary>
/// Entity DTO for creating a blog invitation (repository level)
/// </summary>
public class CreateBlogInvitationEntity
{
    /// <summary>
    /// Token identifier
    /// </summary>
    public Guid TokenId { get; set; }

    /// <summary>
    /// User identifier (who is being invited)
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Token type
    /// </summary>
    public TokenType TokenType { get; set; }

    /// <summary>
    /// Blog identifier (stored in EntityId)
    /// </summary>
    public Guid BlogId { get; set; }

    /// <summary>
    /// Creation timestamp
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }
}
