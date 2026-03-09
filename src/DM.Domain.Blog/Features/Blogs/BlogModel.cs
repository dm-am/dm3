using System;
using System.Collections.Generic;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;

namespace DM.Domain.Blog.Features.Blogs;

/// <summary>
/// DTO for blog
/// </summary>
public class BlogModel
{
    /// <summary>
    /// Blog identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Blog author (owner)
    /// </summary>
    public GeneralUser Author { get; set; } = null!;

    /// <summary>
    /// Blog mentor (for newbie blogs)
    /// </summary>
    public GeneralUser? Mentor { get; set; }

    /// <summary>
    /// Blog title
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// Blog description
    /// </summary>
    public string Description { get; set; } = null!;

    /// <summary>
    /// Creation date
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Last update date
    /// </summary>
    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>
    /// Draft visibility (Private = only roles, Public = preview visible to all)
    /// </summary>
    public DraftVisibility DraftVisibility { get; set; }

    /// <summary>
    /// Whether comments are enabled
    /// </summary>
    public bool CommentsEnabled { get; set; }

    /// <summary>
    /// Total publication count
    /// </summary>
    public int PublicationCount { get; set; }

    /// <summary>
    /// Comment count on the blog itself (denormalized)
    /// </summary>
    public int CommentCount { get; set; }

    /// <summary>
    /// Total comments count (to blog + all publications)
    /// </summary>
    public int CommentsCount { get; set; }

    /// <summary>
    /// Last comment identifier for navigation
    /// </summary>
    public Guid? LastCommentId { get; set; }

    /// <summary>
    /// Blog rubrics (categories)
    /// </summary>
    public IEnumerable<Rubric> Rubrics { get; set; } = [];

    /// <summary>
    /// Blog assistants (for authorization checks)
    /// </summary>
    public IEnumerable<BlogAssistantInfo> Assistants { get; set; } = [];

    /// <summary>
    /// Blog subscriber (reader) IDs from Subscriptions table
    /// </summary>
    public IReadOnlySet<Guid> SubscriberIds { get; set; } = new HashSet<Guid>();

    /// <summary>
    /// User ids with pending invitations (assistant or reader)
    /// </summary>
    public IEnumerable<Guid> PendingInvitedUserIds { get; set; } = [];

    /// <summary>
    /// User IDs who are blacklisted from commenting
    /// </summary>
    public IReadOnlySet<Guid> BlacklistedUserIds { get; set; } = new HashSet<Guid>();
}

/// <summary>
/// Simplified blog assistant info for authorization checks
/// </summary>
public class BlogAssistantInfo
{
    /// <summary>
    /// User identifier
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// When the assistant joined
    /// </summary>
    public DateTimeOffset JoinedUtc { get; set; }
}

/// <summary>
/// DTO for rubric
/// </summary>
public class Rubric
{
    /// <summary>
    /// Rubric identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Rubric title
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// Sort order
    /// </summary>
    public int SortOrder { get; set; }
}
