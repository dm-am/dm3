using System;
using System.Collections.Generic;
using DM.Domain.Core.Enums;

namespace DM.Domain.Blog.Features.Blogs;

/// <summary>
/// Filter and sort for the public blog list.
/// </summary>
public record BlogFilter
{
    /// <summary>
    /// Optional text search by title (fuzzy matching)
    /// </summary>
    public string? Search { get; init; }

    /// <summary>
    /// Optional status filter, OR between the values. Empty or null = all.
    /// </summary>
    public IReadOnlyCollection<ModuleStatus>? Statuses { get; init; }

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

    public DateTimeOffset? CreatedFromUtc { get; init; }

    public DateTimeOffset? CreatedToUtc { get; init; }

    public DateTimeOffset? ActivatedFromUtc { get; init; }

    public DateTimeOffset? ActivatedToUtc { get; init; }

    public DateTimeOffset? ClosedFromUtc { get; init; }

    public DateTimeOffset? ClosedToUtc { get; init; }

    /// <summary>
    /// Owners whose blogs are left out. Not a filter the caller chooses: the
    /// service fills it from the viewer's personal blacklist when the viewer
    /// switched on "Скрывать блоги" (UserBlacklistSettings.HideBlogs).
    /// </summary>
    public IReadOnlyCollection<Guid>? ExcludeOwnerIds { get; init; }

    /// <summary>
    /// Explicit premoderation filter, OR between the values. The service clears
    /// it below Mentor; when set it replaces the default premoderation
    /// visibility restriction.
    /// </summary>
    public IReadOnlyCollection<PremoderationStatus>? PremoderationStatuses { get; init; }

    /// <summary>
    /// Current user id for premoderation visibility, filled by the service
    /// (<see cref="Guid.Empty" /> for guests)
    /// </summary>
    public Guid CurrentUserId { get; init; }
}
