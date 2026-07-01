using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Domain.Community.Features.Awards;

/// <summary>
/// Сервис каталога наград и выдач. RBAC проверяется на уровне
/// контроллеров (RequireRole атрибут) — сервис трастит вызывающий код.
/// </summary>
public interface IAwardService
{
    // ---- Каталог типов наград (timeless) ----

    /// <summary>Все типы наград. Inactive по умолчанию скрыты.</summary>
    Task<IReadOnlyCollection<AwardType>> GetTypesAsync(bool includeInactive = false, CancellationToken ct = default);
    /// <summary>Создать новый тип награды. Валидирует уникальность Code и валидность IconName.</summary>
    Task<AwardType> CreateTypeAsync(CreateAwardType create, CancellationToken ct = default);
    /// <summary>Частично обновить тип награды.</summary>
    Task<AwardType> UpdateTypeAsync(UpdateAwardType update, CancellationToken ct = default);
    /// <summary>Деактивировать тип (IsActive=false). Не удаляет уже выданные награды.</summary>
    Task DeactivateTypeAsync(Guid id, CancellationToken ct = default);

    // ---- Серии конкурсов (ContestSeries) ----

    /// <summary>Все серии конкурсов. Inactive по умолчанию скрыты.</summary>
    Task<IReadOnlyCollection<ContestSeries>> GetSeriesAsync(bool includeInactive = false, CancellationToken ct = default);
    /// <summary>Серия по ID, либо null.</summary>
    Task<ContestSeries?> GetSeriesAsync(Guid id, CancellationToken ct = default);
    /// <summary>Создать новую серию.</summary>
    Task<ContestSeries> CreateSeriesAsync(CreateContestSeries create, CancellationToken ct = default);
    /// <summary>Частично обновить серию.</summary>
    Task<ContestSeries> UpdateSeriesAsync(UpdateContestSeries update, CancellationToken ct = default);
    /// <summary>Деактивировать серию — скрывает ее из dropdown при выдаче.</summary>
    Task DeactivateSeriesAsync(Guid id, CancellationToken ct = default);

    // ---- Выдачи ----

    /// <summary>Список наград пользователя.</summary>
    Task<IReadOnlyCollection<UserAward>> GetUserAwardsAsync(string username, CancellationToken ct = default);
    /// <summary>Выдать награду пользователю. ContestSeriesId опционален (null = внеконкурсная). WorkUrl — ссылка на топик с работой (опц).</summary>
    Task<UserAward> GrantAsync(string username, Guid awardTypeId, Guid? contestSeriesId, string? workUrl, CancellationToken ct = default);
    /// <summary>Отозвать ранее выданную награду (soft-delete).</summary>
    Task RevokeAsync(Guid awardId, CancellationToken ct = default);
}
