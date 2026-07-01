using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Domain.Community.Features.Achievements;

/// <summary>Сервис каталога достижений и lazy-eval engine'а.</summary>
public interface IAchievementService
{
    // ---- Каталог категорий ----

    /// <summary>Список категорий. Inactive скрываются по умолчанию.</summary>
    Task<IReadOnlyCollection<AchievementCategory>> GetCategoriesAsync(bool includeInactive = false, CancellationToken ct = default);

    /// <summary>Частично обновить категорию. Создание/удаление не предусмотрено — каталог статичен.</summary>
    Task<AchievementCategory> UpdateCategoryAsync(UpdateAchievementCategory update, CancellationToken ct = default);

    // ---- Каталог тиров ----

    /// <summary>Получить список тиров. Категория-родитель включена в навигацию.</summary>
    Task<IReadOnlyCollection<AchievementType>> GetTypesAsync(bool includeInactive = false, CancellationToken ct = default);

    /// <summary>Создать тир в существующей категории. Валидирует уникальность Code.</summary>
    Task<AchievementType> CreateTypeAsync(CreateAchievementType create, CancellationToken ct = default);

    /// <summary>Частично обновить тир (Title/Threshold/Tier).</summary>
    Task<AchievementType> UpdateTypeAsync(UpdateAchievementType update, CancellationToken ct = default);

    /// <summary>Жестко удалить тир из каталога. Связанные UserAchievement остаются как историческая запись.</summary>
    Task DeleteTypeAsync(Guid id, CancellationToken ct = default);

    // ---- Получения пользователем ----

    /// <summary>
    /// Получить достижения пользователя с lazy-eval: для каждого активного
    /// тира, который еще не получен, проверить порог. Свежеполученные
    /// INSERT'ятся и добавляются в результат за тот же запрос.
    /// </summary>
    Task<IReadOnlyCollection<UserAchievement>> GetUserAchievementsAsync(string username, CancellationToken ct = default);
}
