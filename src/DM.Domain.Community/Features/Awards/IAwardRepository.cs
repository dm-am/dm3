using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Domain.Community.Features.Awards;

/// <summary>
/// Хранилище для каталога наград, серий конкурсов и записей о выдаче.
/// </summary>
public interface IAwardRepository
{
    // ---- AwardType (timeless catalog) ----

    /// <summary>Все типы наград. Inactive по умолчанию скрыты.</summary>
    Task<IReadOnlyCollection<AwardType>> GetTypesAsync(bool includeInactive, CancellationToken ct = default);
    /// <summary>Тип награды по ID, либо null.</summary>
    Task<AwardType?> GetTypeAsync(Guid id, CancellationToken ct = default);
    /// <summary>Тип награды по стабильному коду, либо null.</summary>
    Task<AwardType?> GetTypeByCodeAsync(string code, CancellationToken ct = default);
    /// <summary>Создать новый тип награды.</summary>
    Task<AwardType> CreateTypeAsync(CreateAwardType create, CancellationToken ct = default);
    /// <summary>Частичное обновление типа награды.</summary>
    Task<AwardType> UpdateTypeAsync(UpdateAwardType update, CancellationToken ct = default);

    // ---- ContestSeries ----

    /// <summary>Все серии конкурсов. Inactive по умолчанию скрыты.</summary>
    Task<IReadOnlyCollection<ContestSeries>> GetSeriesAsync(bool includeInactive, CancellationToken ct = default);
    /// <summary>Серия по ID, либо null.</summary>
    Task<ContestSeries?> GetSeriesAsync(Guid id, CancellationToken ct = default);
    /// <summary>Создать новую серию конкурса.</summary>
    Task<ContestSeries> CreateSeriesAsync(CreateContestSeries create, CancellationToken ct = default);
    /// <summary>Частичное обновление серии.</summary>
    Task<ContestSeries> UpdateSeriesAsync(UpdateContestSeries update, CancellationToken ct = default);

    // ---- UserAward ----

    /// <summary>Все награды пользователя, отсортированные по серии и типу. Только не отозванные.</summary>
    Task<IReadOnlyCollection<UserAward>> GetUserAwardsAsync(Guid userId, CancellationToken ct = default);
    /// <summary>Запись о выдаче по ID, либо null.</summary>
    Task<UserAward?> GetAsync(Guid id, CancellationToken ct = default);
    /// <summary>Создать запись о выдаче (grant).</summary>
    Task<UserAward> CreateAsync(CreateUserAward create, Guid awardedByUserId, CancellationToken ct = default);
    /// <summary>Soft-delete награды (revoke).</summary>
    Task RevokeAsync(Guid id, Guid revokedByUserId, CancellationToken ct = default);
}
