using System;
using System.Collections.Generic;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;

namespace DM.Domain.Blog.Features.Blogs;

/// <summary>
/// DTO for blog (lightweight, for lists)
/// </summary>
public class Blog
{
    /// <summary>
    /// Blog identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Short public identifier for URLs (5 lowercase letters), like games
    /// </summary>
    public string PublicId { get; set; } = string.Empty;

    /// <summary>
    /// Blog author
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
    /// Creation date (UTC)
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Blog status (Draft, Active, Closed)
    /// </summary>
    public ModuleStatus Status { get; set; }

    /// <summary>
    /// Premoderation status for newbie bloggers
    /// </summary>
    public PremoderationStatus PremoderationStatus { get; set; }

    /// <summary>
    /// When the blog was first activated (changed from Draft to Active)
    /// </summary>
    public DateTimeOffset? ActivatedUtc { get; set; }

    /// <summary>
    /// When the blog was closed
    /// </summary>
    public DateTimeOffset? ClosedUtc { get; set; }

    /// <summary>
    /// Reason why the blog was closed (only applicable when Status = Closed)
    /// </summary>
    public ClosedReason ClosedReason { get; set; }

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
    /// Unread publications count
    /// </summary>
    public int UnreadPublicationsCount { get; set; }

    /// <summary>
    /// Unread comments count (to blog + all publications)
    /// </summary>
    public int UnreadCommentsCount { get; set; }

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
    /// Subscriber usernames for tooltip display (first 5)
    /// </summary>
    public IEnumerable<string> SubscriberUsernames { get; set; } = [];

    /// <summary>
    /// Count of active subscribers (readers who were active within last 30 days)
    /// </summary>
    public int ActiveSubscribersCount { get; set; }

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
/// Simplified blog assistant info for authorization checks and display
/// </summary>
public class BlogAssistantInfo
{
    /// <summary>
    /// User identifier
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Username for display
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// When the assistant joined
    /// </summary>
    public DateTimeOffset JoinedUtc { get; set; }

    /// <summary>
    /// Last activity moment (UTC) - for online indicators
    /// </summary>
    public DateTimeOffset? LastActivityUtc { get; set; }

    /// <summary>
    /// User role (for displaying role badges [А], [С], [М], [Н], [Р])
    /// </summary>
    public UserRole Role { get; set; }

    /// <summary>
    /// Whether user is a newbie (less than 100 posts) - affects name color
    /// </summary>
    public bool IsNewbie { get; set; }
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

    /// <summary>
    /// Total published publication count in this rubric. A supplementary
    /// total (not part of the "(N/A)" counter). Computed via a repository
    /// projection subquery.
    /// </summary>
    public int PublicationCount { get; set; }

    /// <summary>
    /// Count of publications in this rubric with unread content for the
    /// current viewer — the "N" in the "(N/A)" counter (doc 4.2.1.4).
    /// Filled in the service layer, mirroring how game rooms fill their
    /// unread counters.
    /// </summary>
    public int UnreadPublicationsCount { get; set; }

    /// <summary>
    /// Total unread comments across this rubric's publications for the
    /// current viewer — the "A" in the "(N/A)" counter (doc 4.2.1.4).
    /// Filled in the service layer (sum of the per-publication unread
    /// counters), mirroring the blog-level UnreadCommentsCount.
    /// </summary>
    public int UnreadCommentsCount { get; set; }
}
