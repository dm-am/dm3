using System;
using System.Collections.Generic;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;

namespace DM.Web.API.Features.Blog.Blogs;

/// <summary>
/// Query parameters for blog list filtering
/// </summary>
public class BlogsQuery : PagingQuery
{
    /// <summary>
    /// Text search by blog title (case-insensitive, fuzzy matching with typo tolerance)
    /// </summary>
    public string? Search { get; set; }

    /// <summary>
    /// Blog statuses to show, OR between them (Draft, Active, Closed). Empty = all.
    /// </summary>
    /// <remarks>
    /// Plural and repeatable, like <c>statuses</c> on the mirrored /v1/games. The
    /// two lists took the same filter under two names and two encodings, and a
    /// consumer that carried <c>status</c> over from here to there got a 200 and
    /// the unfiltered set.
    /// </remarks>
    public IReadOnlyCollection<ModuleStatus>? Statuses { get; set; }

    /// <summary>
    /// Filter by premoderation status, OR between them (Approved, AwaitingApproval,
    /// AwaitingEdits). Honored only for Mentor+ callers (premoderation review
    /// queue); silently ignored otherwise.
    /// </summary>
    public IReadOnlyCollection<PremoderationStatus>? PremoderationStatuses { get; set; }

    /// <summary>
    /// Sort field: title, status, popularity, created (default), activated, closed
    /// </summary>
    public string? SortBy { get; set; }

    /// <summary>
    /// Sort order: asc or desc (default: desc)
    /// </summary>
    public string? SortOrder { get; set; }

    /// <summary>
    /// If true, returns only blogs where current user participates
    /// (owner, mentor, assistant, or reader). Requires authentication.
    /// </summary>
    public bool? Participating { get; set; }

    /// <summary>
    /// Filter by host usernames - returns blogs where user is owner OR assistant (case-insensitive, OR logic)
    /// </summary>
    /// <remarks>
    /// Same name as on /v1/games. "Who runs it" and "who wrote it" are two
    /// filters, so the hosts of a game are not <c>authorUsernames</c> either.
    /// </remarks>
    public IReadOnlyCollection<string>? HostUsernames { get; set; }

    /// <summary>
    /// Created date range start (inclusive)
    /// </summary>
    public DateTimeOffset? CreatedFromUtc { get; set; }

    /// <summary>
    /// Created date range end (inclusive)
    /// </summary>
    public DateTimeOffset? CreatedToUtc { get; set; }

    /// <summary>
    /// Activated date range start (inclusive). Blogs without ActivatedUtc are excluded.
    /// </summary>
    public DateTimeOffset? ActivatedFromUtc { get; set; }

    /// <summary>
    /// Activated date range end (inclusive). Blogs without ActivatedUtc are excluded.
    /// </summary>
    public DateTimeOffset? ActivatedToUtc { get; set; }

    /// <summary>
    /// Closed date range start (inclusive). Blogs without ClosedUtc are excluded.
    /// </summary>
    public DateTimeOffset? ClosedFromUtc { get; set; }

    /// <summary>
    /// Closed date range end (inclusive). Blogs without ClosedUtc are excluded.
    /// </summary>
    public DateTimeOffset? ClosedToUtc { get; set; }

    /// <summary>
    /// Response projection: "full" (default) includes rubrics array,
    /// "ref" returns lightweight BlogRef without rubrics (for sidebars/menus).
    /// </summary>
    public string? Projection { get; set; }
}
