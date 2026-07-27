using System;
using DM.Domain.Core.Dto;

namespace DM.Domain.Community.Features.Achievements;

/// <summary>
/// The fact that a user earned an achievement. Created only by the evaluator
/// (either lazily on read, or by the event-driven worker in Phase 2).
/// There is no manual grant — the only way to earn one is to cross
/// the metric threshold. UNIQUE(UserId, AchievementTypeId) guarantees
/// idempotency of repeated evaluations.
/// </summary>
public class UserAchievement
{
    /// <summary>Identifier of the earned record.</summary>
    public Guid Id { get; set; }
    /// <summary>User who earned the achievement.</summary>
    public GeneralUser User { get; set; } = null!;
    /// <summary>Achievement type from the catalog.</summary>
    public AchievementType Type { get; set; } = null!;
    /// <summary>Moment the threshold was crossed (UTC).</summary>
    public DateTimeOffset EarnedUtc { get; set; }
}
