using System;
using System.Collections.Generic;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;

namespace DM.Domain.Game.Features.Games;

/// <summary>
/// Query parameters for game list filtering
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
    /// Filter by owner usernames - master OR assistant (case-insensitive, OR logic)
    /// </summary>
    public IReadOnlyCollection<string>? OwnerUsernames { get; set; }

    /// <summary>
    /// Filter by player username (case-insensitive); the matched participation
    /// kind is controlled by PlayerParticipation (active characters by default)
    /// </summary>
    public string? PlayerUsername { get; set; }

    /// <summary>
    /// Participation scope for the PlayerUsername filter. Null/Active (default) -
    /// user has an active character. Any - user has any non-NPC character except
    /// declined applications (including retired ones and applications under review).
    /// Ignored if PlayerUsername is not set.
    /// </summary>
    public PlayerParticipation? PlayerParticipation { get; set; }

    /// <summary>
    /// Created date range start (inclusive)
    /// </summary>
    public DateTimeOffset? CreatedFromUtc { get; set; }

    /// <summary>
    /// Created date range end (inclusive)
    /// </summary>
    public DateTimeOffset? CreatedToUtc { get; set; }

    /// <summary>
    /// Activated date range start (inclusive). Games without ActivatedUtc are excluded.
    /// </summary>
    public DateTimeOffset? ActivatedFromUtc { get; set; }

    /// <summary>
    /// Activated date range end (inclusive). Games without ActivatedUtc are excluded.
    /// </summary>
    public DateTimeOffset? ActivatedToUtc { get; set; }

    /// <summary>
    /// Closed date range start (inclusive). Games without ClosedUtc are excluded.
    /// </summary>
    public DateTimeOffset? ClosedFromUtc { get; set; }

    /// <summary>
    /// Closed date range end (inclusive). Games without ClosedUtc are excluded.
    /// </summary>
    public DateTimeOffset? ClosedToUtc { get; set; }

    /// <summary>
    /// Recruitment started date range start (inclusive). Games without RecruitmentStartedUtc are excluded.
    /// </summary>
    public DateTimeOffset? RecruitmentStartedFromUtc { get; set; }

    /// <summary>
    /// Recruitment started date range end (inclusive). Games without RecruitmentStartedUtc are excluded.
    /// </summary>
    public DateTimeOffset? RecruitmentStartedToUtc { get; set; }

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
    /// (reader, player, assistant, mentor, or master).
    /// </summary>
    public bool? Participating { get; set; }

    /// <summary>
    /// Premoderation statuses to show (OR between selected). Moderation-only
    /// filter: honored for Mentor+ callers, forbidden otherwise. When set,
    /// the per-user accessibility scope is replaced with the moderation scope
    /// (all non-removed games), because the public scope hides exactly the
    /// premoderated games this filter targets.
    /// </summary>
    public IReadOnlyCollection<PremoderationStatus>? PremoderationStatuses { get; set; }
}
