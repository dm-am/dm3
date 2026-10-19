using System;
using DM.Domain.Core.Dto;

namespace DM.Domain.Community.Features.Achievements;

/// <summary>
/// The fact that a user earned an achievement, created only by the evaluation on
/// read. There is no manual grant — the only way to earn one is to cross the
/// metric threshold. UNIQUE(UserId, AchievementTypeId) guarantees idempotency of
/// repeated evaluations.
/// </summary>
public class UserAchievement
{
    public Guid Id { get; set; }
    public GeneralUser User { get; set; } = null!;
    public AchievementType Type { get; set; } = null!;
    /// <summary>Moment the threshold was crossed (UTC).</summary>
    public DateTimeOffset EarnedUtc { get; set; }
}
