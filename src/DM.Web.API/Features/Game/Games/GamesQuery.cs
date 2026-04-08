using System;
using System.Collections.Generic;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;

namespace DM.Web.API.Features.Game.Games;

/// <summary>
/// Input DTO for games filtering
/// </summary>
public class GamesQuery : PagingQuery
{
    /// <summary>
    /// Text search (title, system, setting) - OR between fields
    /// </summary>
    public string? Search { get; set; }

    /// <summary>
    /// Game statuses to show (OR between selected). Empty = all statuses.
    /// </summary>
    public IReadOnlyCollection<ModuleStatus>? Statuses { get; set; }

    /// <summary>
    /// Recruitment filter for Active games. Ignored if Active not in Statuses.
    /// </summary>
    public RecruitmentFilter? RecruitmentFilter { get; set; }

    /// <summary>
    /// Closed reason filter for Closed games. Ignored if Closed not in Statuses.
    /// </summary>
    public ClosedReason? ClosedReasonFilter { get; set; }

    /// <summary>
    /// Required tag IDs - game must have ALL of them (AND logic)
    /// </summary>
    public IReadOnlyCollection<int>? RequiredTags { get; set; }

    /// <summary>
    /// Optional tag IDs - game must have at least ONE of them (OR logic)
    /// </summary>
    public IReadOnlyCollection<int>? OptionalTags { get; set; }

    /// <summary>
    /// Excluded tag IDs - game must NOT have any of them (NOR logic)
    /// </summary>
    public IReadOnlyCollection<int>? ExcludedTags { get; set; }

    /// <summary>
    /// Filter by author usernames - master OR assistant (case-insensitive, OR logic)
    /// </summary>
    public IReadOnlyCollection<string>? AuthorUsernames { get; set; }

    /// <summary>
    /// Filter by player username - user has active character (case-insensitive)
    /// </summary>
    public string? PlayerUsername { get; set; }

    /// <summary>
    /// Created date range start (inclusive)
    /// </summary>
    public DateTimeOffset? CreatedFrom { get; set; }

    /// <summary>
    /// Created date range end (inclusive)
    /// </summary>
    public DateTimeOffset? CreatedTo { get; set; }

    /// <summary>
    /// Activated date range start (inclusive). Games without ActivatedUtc are excluded.
    /// </summary>
    public DateTimeOffset? ActivatedFrom { get; set; }

    /// <summary>
    /// Activated date range end (inclusive). Games without ActivatedUtc are excluded.
    /// </summary>
    public DateTimeOffset? ActivatedTo { get; set; }

    /// <summary>
    /// Closed date range start (inclusive). Games without ClosedUtc are excluded.
    /// </summary>
    public DateTimeOffset? ClosedFrom { get; set; }

    /// <summary>
    /// Closed date range end (inclusive). Games without ClosedUtc are excluded.
    /// </summary>
    public DateTimeOffset? ClosedTo { get; set; }

    /// <summary>
    /// Recruitment started date range start (inclusive). Games without RecruitmentStartedUtc are excluded.
    /// </summary>
    public DateTimeOffset? RecruitmentStartedFrom { get; set; }

    /// <summary>
    /// Recruitment started date range end (inclusive). Games without RecruitmentStartedUtc are excluded.
    /// </summary>
    public DateTimeOffset? RecruitmentStartedTo { get; set; }

    /// <summary>
    /// Sort field: created, activated, recruitmentstarted, title, popularity, status, availableslots, closed
    /// </summary>
    public string? SortBy { get; set; }

    /// <summary>
    /// Sort order: asc or desc (default: desc)
    /// </summary>
    public string? SortOrder { get; set; }

    /// <summary>
    /// If true, returns only games where current user participates
    /// (reader, player, assistant, mentor, or master). Requires authentication.
    /// </summary>
    public bool? Participating { get; set; }

    /// <summary>
    /// Response projection: "full" (default) includes players/readers arrays for tooltips,
    /// "ref" returns lightweight GameRef with counts only (for sidebars/menus).
    /// </summary>
    public string? Projection { get; set; }
}
