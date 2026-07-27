using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Domain.Community.Features.Awards;

/// <summary>
/// Storage for the award catalog, contest series and grant records.
/// </summary>
public interface IAwardRepository
{
    // ---- AwardType (timeless catalog) ----

    /// <summary>All award types. Inactive ones are hidden by default.</summary>
    Task<IReadOnlyCollection<AwardType>> GetTypesAsync(bool includeInactive, CancellationToken ct = default);
    /// <summary>Award type by ID, or null.</summary>
    Task<AwardType?> GetTypeAsync(Guid id, CancellationToken ct = default);
    /// <summary>Award type by stable code, or null.</summary>
    Task<AwardType?> GetTypeByCodeAsync(string code, CancellationToken ct = default);
    /// <summary>Create a new award type.</summary>
    Task<AwardType> CreateTypeAsync(CreateAwardType create, CancellationToken ct = default);
    /// <summary>Partial award type update.</summary>
    Task<AwardType> UpdateTypeAsync(UpdateAwardType update, CancellationToken ct = default);

    // ---- ContestSeries ----

    /// <summary>All contest series. Inactive ones are hidden by default.</summary>
    Task<IReadOnlyCollection<ContestSeries>> GetSeriesAsync(bool includeInactive, CancellationToken ct = default);
    /// <summary>Series by ID, or null.</summary>
    Task<ContestSeries?> GetSeriesAsync(Guid id, CancellationToken ct = default);
    /// <summary>Create a new contest series.</summary>
    Task<ContestSeries> CreateSeriesAsync(CreateContestSeries create, CancellationToken ct = default);
    /// <summary>Partial series update.</summary>
    Task<ContestSeries> UpdateSeriesAsync(UpdateContestSeries update, CancellationToken ct = default);

    // ---- UserAward ----

    /// <summary>All awards of a user, sorted by series and type. Non-revoked only.</summary>
    Task<IReadOnlyCollection<UserAward>> GetUserAwardsAsync(Guid userId, CancellationToken ct = default);
    /// <summary>Grant record by ID, or null.</summary>
    Task<UserAward?> GetAsync(Guid id, CancellationToken ct = default);
    /// <summary>Create a grant record.</summary>
    Task<UserAward> CreateAsync(CreateUserAward create, Guid awardedByUserId, CancellationToken ct = default);
    /// <summary>Soft-delete an award (revoke).</summary>
    Task RevokeAsync(Guid id, Guid revokedByUserId, CancellationToken ct = default);
}
