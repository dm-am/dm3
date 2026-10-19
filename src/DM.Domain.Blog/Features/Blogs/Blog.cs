using System;
using System.Collections.Generic;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;

namespace DM.Domain.Blog.Features.Blogs;

public class Blog
{
    public Guid Id { get; set; }

    /// <summary>
    /// Short public identifier for URLs (5 lowercase letters), like games
    /// </summary>
    public string PublicId { get; set; } = string.Empty;

    public GeneralUser Author { get; set; } = null!;

    public GeneralUser? Mentor { get; set; }

    public string Title { get; set; } = null!;

    public string Description { get; set; } = null!;

    public DateTimeOffset CreatedUtc { get; set; }

    public ModuleStatus Status { get; set; }

    public PremoderationStatus PremoderationStatus { get; set; }

    /// <summary>
    /// When the blog was first activated (changed from Draft to Active)
    /// </summary>
    public DateTimeOffset? ActivatedUtc { get; set; }

    public DateTimeOffset? ClosedUtc { get; set; }

    /// <summary>
    /// Only applicable when Status = Closed
    /// </summary>
    public ClosedReason ClosedReason { get; set; }

    /// <summary>
    /// Private = only roles, Public = preview visible to all
    /// </summary>
    public DraftVisibility DraftVisibility { get; set; }

    public int PublicationCount { get; set; }

    /// <summary>
    /// Total comments count (to blog + all publications)
    /// </summary>
    public int CommentsCount { get; set; }

    public int UnreadPublicationsCount { get; set; }

    /// <summary>
    /// Unread comments count (to blog + all publications)
    /// </summary>
    public int UnreadCommentsCount { get; set; }

    public Guid? LastCommentId { get; set; }

    public IEnumerable<Rubric> Rubrics { get; set; } = [];

    public IEnumerable<BlogAssistantInfo> Assistants { get; set; } = [];

    public int SubscribersCount { get; set; }

    /// <summary>
    /// Whether the user this blog was read for subscribes to it.
    /// </summary>
    public bool IsViewerSubscriber { get; set; }

    /// <summary>
    /// Subscriber usernames for tooltip display, capped and ordered per
    /// <see cref="SubscriptionPolicy.PreviewCap" />
    /// </summary>
    public IEnumerable<string> SubscriberUsernames { get; set; } = [];

    /// <summary>
    /// Readers who were active within last 30 days
    /// </summary>
    public int ActiveSubscribersCount { get; set; }

    /// <summary>
    /// User ids with pending invitations (assistant or reader)
    /// </summary>
    public IEnumerable<Guid> PendingInvitedUserIds { get; set; } = [];

    /// <summary>
    /// The users on the blog's blacklist, filled by every blog read. Asked only
    /// through BlogRoleExtensions.IsBlacklisted.
    /// </summary>
    public IReadOnlyCollection<Guid> BlacklistedUserIds { get; set; } = [];
}

public class BlogAssistantInfo
{
    public Guid UserId { get; set; }

    public string Username { get; set; } = string.Empty;

    public DateTimeOffset JoinedUtc { get; set; }

    public DateTimeOffset? LastActivityUtc { get; set; }

    /// <summary>
    /// For the role badges "[А]", "[С]", "[М]", "[Н]", "[Р]"
    /// </summary>
    public UserRole Role { get; set; }

    /// <summary>
    /// Less than 100 posts, which colors the name
    /// </summary>
    public bool IsNewbie { get; set; }
}

public class Rubric
{
    public Guid Id { get; set; }

    public string Title { get; set; } = null!;

    public int SortOrder { get; set; }

    /// <summary>
    /// Total published publication count in this rubric, not part of the "(N/A)" counter.
    /// </summary>
    public int PublicationCount { get; set; }

    /// <summary>
    /// Publications in this rubric with unread content for the current viewer,
    /// the "N" in the "(N/A)" counter.
    /// </summary>
    public int UnreadPublicationsCount { get; set; }

    /// <summary>
    /// Unread comments across this rubric's publications for the current viewer,
    /// the "A" in the "(N/A)" counter.
    /// </summary>
    public int UnreadCommentsCount { get; set; }
}
