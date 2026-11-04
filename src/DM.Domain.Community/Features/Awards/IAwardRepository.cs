using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Domain.Community.Features.Awards;

/// <summary>
/// Storage for the award catalog, contest series and grant records.
/// </summary>
/// <remarks>
/// The mutating methods (Update*, Revoke) assume the row exists and throw on an
/// absent one. A caller that skips the existence check turns an unknown id into
/// a 500 where the endpoint declares 404.
/// </remarks>
public interface IAwardRepository
{
    /// <summary>All award types. Inactive ones are hidden by default.</summary>
    Task<IReadOnlyCollection<AwardType>> GetTypesAsync(bool includeInactive, CancellationToken ct = default);
    Task<AwardType?> GetTypeAsync(Guid id, CancellationToken ct = default);
    Task<AwardType?> GetTypeByCodeAsync(string code, CancellationToken ct = default);
    Task<AwardType> CreateTypeAsync(CreateAwardType create, CancellationToken ct = default);
    Task<AwardType> UpdateTypeAsync(UpdateAwardType update, CancellationToken ct = default);

    /// <summary>All contest series. Inactive ones are hidden by default.</summary>
    Task<IReadOnlyCollection<ContestSeries>> GetSeriesAsync(bool includeInactive, CancellationToken ct = default);
    Task<ContestSeries?> GetSeriesAsync(Guid id, CancellationToken ct = default);
    Task<ContestSeries> CreateSeriesAsync(CreateContestSeries create, CancellationToken ct = default);
    Task<ContestSeries> UpdateSeriesAsync(UpdateContestSeries update, CancellationToken ct = default);

    /// <summary>All awards of a user, sorted by series and type. Non-revoked only.</summary>
    Task<IReadOnlyCollection<UserAward>> GetUserAwardsAsync(Guid userId, CancellationToken ct = default);
    /// <summary>All awards granted within a contest series, recipients included. Non-revoked only.</summary>
    Task<IReadOnlyCollection<UserAward>> GetSeriesAwardsAsync(Guid seriesId, CancellationToken ct = default);
    Task<UserAward?> GetAsync(Guid id, CancellationToken ct = default);
    Task<UserAward> CreateAsync(CreateUserAward create, Guid awardedByUserId, CancellationToken ct = default);
    /// <summary>Soft-delete an award (revoke).</summary>
    Task RevokeAsync(Guid id, Guid revokedByUserId, CancellationToken ct = default);
}
