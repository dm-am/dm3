using System;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;

namespace DM.Domain.Blog.Features.Invitations;

/// <summary>
/// Pending blog invitation
/// </summary>
public class BlogInvitation
{
    public Guid TokenId { get; set; }

    public Guid BlogId { get; set; }

    public string BlogTitle { get; set; } = null!;

    public GeneralUser InvitedUser { get; set; } = null!;

    public GeneralUser InvitedBy { get; set; } = null!;

    /// <summary>
    /// Reader or Assistant
    /// </summary>
    public BlogRole TargetRole { get; set; }

    public DateTimeOffset CreatedUtc { get; set; }

    public DateTimeOffset ExpiresUtc { get; set; }
}

public class CreateBlogInvitationEntity
{
    public Guid TokenId { get; set; }

    /// <summary>
    /// Who is being invited
    /// </summary>
    public Guid UserId { get; set; }

    public TokenType TokenType { get; set; }

    /// <summary>
    /// Stored in EntityId
    /// </summary>
    public Guid BlogId { get; set; }

    public DateTimeOffset CreatedUtc { get; set; }
}
