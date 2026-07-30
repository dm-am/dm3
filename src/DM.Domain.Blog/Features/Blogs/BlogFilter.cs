using System;
using System.Collections.Generic;
using DM.Domain.Core.Enums;

namespace DM.Domain.Blog.Features.Blogs;

/// <summary>
/// Filter and sort for the public blog list. The list and its total count take
/// this one object instead of a dozen positional arguments each: the two can no
/// longer be filtered differently by accident, and adding a criterion is a
/// property here rather than a new argument threaded through three call sites
/// where two same-typed timestamps sit next to each other.
/// </summary>
public record BlogFilter
{
    /// <summary>
    /// Optional text search by title (fuzzy matching)
    /// </summary>
    public string? Search { get; init; }

    /// <summary>
    /// Optional status filter
    /// </summary>
    public ModuleStatus? Status { get; init; }

    /// <summary>
    /// Optional host usernames as the caller wrote them (owner or assistant, OR logic)
    /// </summary>
    public IReadOnlyCollection<string>? HostUsernames { get; init; }

    /// <summary>
    /// Host user ids the service resolved from <see cref="HostUsernames" />.
    /// The query filters on these — usernames never reach the database.
    /// </summary>
    public IReadOnlyCollection<Guid>? HostUserIds { get; init; }

    /// <summary>
    /// Sort field: title, status, popularity, created (default), activated, closed.
    /// Ignored by the count, which needs the same filter but no order.
    /// </summary>
    public string? SortBy { get; init; }

    /// <summary>
    /// Sort direction: asc or desc (default: desc)
    /// </summary>
    public string? SortOrder { get; init; }

    /// <summary>
    /// Created date range start
    /// </summary>
    public DateTimeOffset? CreatedFromUtc { get; init; }

    /// <summary>
    /// Created date range end
    /// </summary>
    public DateTimeOffset? CreatedToUtc { get; init; }

    /// <summary>
    /// Activated date range start
    /// </summary>
    public DateTimeOffset? ActivatedFromUtc { get; init; }

    /// <summary>
    /// Activated date range end
    /// </summary>
    public DateTimeOffset? ActivatedToUtc { get; init; }

    /// <summary>
    /// Closed date range start
    /// </summary>
    public DateTimeOffset? ClosedFromUtc { get; init; }

    /// <summary>
    /// Closed date range end
    /// </summary>
    public DateTimeOffset? ClosedToUtc { get; init; }

    /// <summary>
    /// Optional owner ids to exclude (for blacklist filtering)
    /// </summary>
    public IReadOnlyCollection<Guid>? ExcludeOwnerIds { get; init; }

    /// <summary>
    /// Explicit premoderation filter. The service clears it below Mentor;
    /// when set it replaces the default premoderation visibility restriction.
    /// </summary>
    public PremoderationStatus? PremoderationStatus { get; init; }

    /// <summary>
    /// Current user id for premoderation visibility, filled by the service
    /// (<see cref="Guid.Empty" /> for guests)
    /// </summary>
    public Guid CurrentUserId { get; init; }
}
