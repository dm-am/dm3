using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Domain.Community.Features.Awards;

/// <summary>
/// Service for the award catalog and grants. RBAC is checked at the
/// controller level (RequireRole attribute) — the service trusts the caller.
/// </summary>
public interface IAwardService
{
    // ---- Award type catalog (timeless) ----

    /// <summary>All award types. Inactive ones are hidden by default.</summary>
    Task<IReadOnlyCollection<AwardType>> GetTypesAsync(bool includeInactive = false, CancellationToken ct = default);
    /// <summary>Create a new award type. Validates Code uniqueness and IconName validity.</summary>
    Task<AwardType> CreateTypeAsync(CreateAwardType create, CancellationToken ct = default);
    /// <summary>Partially update an award type.</summary>
    Task<AwardType> UpdateTypeAsync(UpdateAwardType update, CancellationToken ct = default);
    /// <summary>Deactivate a type (IsActive=false). Does not delete already granted awards.</summary>
    Task DeactivateTypeAsync(Guid id, CancellationToken ct = default);

    // ---- Contest series (ContestSeries) ----

    /// <summary>All contest series. Inactive ones are hidden by default.</summary>
    Task<IReadOnlyCollection<ContestSeries>> GetSeriesAsync(bool includeInactive = false, CancellationToken ct = default);
    /// <summary>Series by ID, or null.</summary>
    Task<ContestSeries?> GetSeriesAsync(Guid id, CancellationToken ct = default);
    /// <summary>Create a new series.</summary>
    Task<ContestSeries> CreateSeriesAsync(CreateContestSeries create, CancellationToken ct = default);
    /// <summary>Partially update a series.</summary>
    Task<ContestSeries> UpdateSeriesAsync(UpdateContestSeries update, CancellationToken ct = default);
    /// <summary>Deactivate a series — hides it from the grant dropdown.</summary>
    Task DeactivateSeriesAsync(Guid id, CancellationToken ct = default);

    // ---- Grants ----

    /// <summary>List of a user's awards.</summary>
    Task<IReadOnlyCollection<UserAward>> GetUserAwardsAsync(string username, CancellationToken ct = default);
    /// <summary>Everyone awarded within a contest series — the moderator view of "who won this contest".</summary>
    Task<IReadOnlyCollection<UserAward>> GetSeriesAwardsAsync(Guid seriesId, CancellationToken ct = default);
    /// <summary>Grant an award to a user. ContestSeriesId is optional (null = outside a contest). WorkUrl is a link to the topic with the work (optional).</summary>
    Task<UserAward> GrantAsync(string username, Guid awardTypeId, Guid? contestSeriesId, string? workUrl, CancellationToken ct = default);
    /// <summary>Revoke a previously granted award (soft-delete).</summary>
    Task RevokeAsync(Guid awardId, CancellationToken ct = default);
}
