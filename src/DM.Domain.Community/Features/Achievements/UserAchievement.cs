using System;
using DM.Domain.Core.Dto;

namespace DM.Domain.Community.Features.Achievements;

/// <summary>
/// Факт получения достижения пользователем. Создается только evaluator'ом
/// (либо lazy на чтении, либо event-driven worker'ом в Phase 2).
/// Ручной grant отсутствует — единственный способ получить — пересечь
/// порог метрики. UNIQUE(UserId, AchievementTypeId) гарантирует
/// идемпотентность повторных оценок.
/// </summary>
public class UserAchievement
{
    /// <summary>Идентификатор записи о получении.</summary>
    public Guid Id { get; set; }
    /// <summary>Пользователь, получивший достижение.</summary>
    public GeneralUser User { get; set; } = null!;
    /// <summary>Тип достижения из каталога.</summary>
    public AchievementType Type { get; set; } = null!;
    /// <summary>Момент пересечения порога (UTC).</summary>
    public DateTimeOffset EarnedUtc { get; set; }
}
