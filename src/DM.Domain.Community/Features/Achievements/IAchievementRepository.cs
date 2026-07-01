using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Domain.Community.Features.Achievements;

/// <summary>Хранилище каталога достижений (категории, тиры) и фактов получения.</summary>
public interface IAchievementRepository
{
    // ---- Категории ----

    /// <summary>Все категории. Inactive по умолчанию скрыты.</summary>
    Task<IReadOnlyCollection<AchievementCategory>> GetCategoriesAsync(bool includeInactive, CancellationToken ct = default);
    /// <summary>Категория по ID, либо null.</summary>
    Task<AchievementCategory?> GetCategoryAsync(Guid id, CancellationToken ct = default);
    /// <summary>Частичное обновление категории.</summary>
    Task<AchievementCategory> UpdateCategoryAsync(UpdateAchievementCategory update, CancellationToken ct = default);

    // ---- Тиры ----

    /// <summary>Все тиры с включенной навигацией Category. Inactive (по флагу категории) по умолчанию скрыты.</summary>
    Task<IReadOnlyCollection<AchievementType>> GetTypesAsync(bool includeInactive, CancellationToken ct = default);
    /// <summary>Тир по ID, либо null.</summary>
    Task<AchievementType?> GetTypeAsync(Guid id, CancellationToken ct = default);
    /// <summary>Тир по стабильному коду, либо null.</summary>
    Task<AchievementType?> GetTypeByCodeAsync(string code, CancellationToken ct = default);
    /// <summary>Создать новый тир в существующей категории.</summary>
    Task<AchievementType> CreateTypeAsync(CreateAchievementType create, CancellationToken ct = default);
    /// <summary>Частичное обновление тира.</summary>
    Task<AchievementType> UpdateTypeAsync(UpdateAchievementType update, CancellationToken ct = default);
    /// <summary>Жесткое удаление тира из каталога.</summary>
    Task DeleteTypeAsync(Guid id, CancellationToken ct = default);

    // ---- Получения ----

    /// <summary>Все достижения пользователя, новые сверху.</summary>
    Task<IReadOnlyCollection<UserAchievement>> GetUserAchievementsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Идемпотентная вставка. true = реально создана; false = уже была (UNIQUE constraint).</summary>
    Task<bool> TryGrantAsync(Guid userId, Guid achievementTypeId, DateTimeOffset earnedUtc, CancellationToken ct = default);
}
