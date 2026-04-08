using System;
using System.Collections.Generic;
using DM.Domain.Core.Enums;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Blog.Blogs;

/// <summary>
/// Lightweight blog reference for sidebars and menus.
/// Unlike full Blog, omits rubrics and other detail-page-only fields.
/// </summary>
public class BlogRef
{
    /// <summary>
    /// Blog unique identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Blog title
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Blog author (lightweight reference)
    /// </summary>
    public UserRef Author { get; set; } = null!;

    /// <summary>
    /// Blog assistants (lightweight references for tooltip)
    /// </summary>
    public IEnumerable<UserRef> Assistants { get; set; } = [];

    /// <summary>
    /// Blog status (Draft, Active, Closed)
    /// </summary>
    public ModuleStatus Status { get; set; }

    /// <summary>
    /// When the blog was created
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// When the blog was first activated
    /// </summary>
    public DateTimeOffset? ActivatedUtc { get; set; }

    /// <summary>
    /// When the blog was closed
    /// </summary>
    public DateTimeOffset? ClosedUtc { get; set; }

    /// <summary>
    /// Number of blog subscribers
    /// </summary>
    public int SubscribersCount { get; set; }

    /// <summary>
    /// Subscriber usernames for tooltip display (first 5)
    /// </summary>
    public IEnumerable<string> SubscriberUsernames { get; set; } = [];

    /// <summary>
    /// Number of active subscribers (readers active within last 30 days)
    /// </summary>
    public int ActiveSubscribersCount { get; set; }

    /// <summary>
    /// Unread publications count (for current user)
    /// </summary>
    public int UnreadPublicationsCount { get; set; }

    /// <summary>
    /// Unread comments count (for current user)
    /// </summary>
    public int UnreadCommentsCount { get; set; }
}
