using System;
using System.Threading.Tasks;
using DM.Web.API.Features.Community.Awards;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Moderation.Awards;

/// <summary>
/// API service for the award type catalog and contest series
/// </summary>
public interface IAwardCatalogApiService
{
    /// <summary>
    /// Get the full award type catalog, inactive included
    /// </summary>
    /// <remarks>
    /// The public catalog serves active records only, so a deactivated type
    /// vanished from the admin page together with its "restore" button. The
    /// admin list is the one place a hidden record must stay visible.
    /// </remarks>
    Task<ListEnvelope<AwardType>> GetTypes();

    /// <summary>
    /// Get all contest series, inactive included
    /// </summary>
    Task<ListEnvelope<ContestSeries>> GetSeries();

    /// <summary>
    /// Create a new award type
    /// </summary>
    Task<Envelope<AwardType>> CreateType(CreateAwardTypeRequest request);

    /// <summary>
    /// Partially update an award type
    /// </summary>
    /// <param name="id">Award type identifier</param>
    /// <param name="request">Fields to update</param>
    Task<Envelope<AwardType>> UpdateType(Guid id, UpdateAwardTypeRequest request);

    /// <summary>
    /// Deactivate an award type, keeping already granted awards
    /// </summary>
    /// <param name="id">Award type identifier</param>
    Task DeactivateType(Guid id);

    /// <summary>
    /// Get everyone awarded within a contest series
    /// </summary>
    /// <param name="seriesId">Series identifier</param>
    Task<ListEnvelope<ContestSeriesAward>> GetSeriesAwards(Guid seriesId);

    /// <summary>
    /// Create a new contest series
    /// </summary>
    Task<Envelope<ContestSeries>> CreateSeries(CreateContestSeriesRequest request);

    /// <summary>
    /// Partially update a contest series
    /// </summary>
    /// <param name="id">Series identifier</param>
    /// <param name="request">Fields to update</param>
    Task<Envelope<ContestSeries>> UpdateSeries(Guid id, UpdateContestSeriesRequest request);

    /// <summary>
    /// Deactivate a contest series, keeping already granted awards
    /// </summary>
    /// <param name="id">Series identifier</param>
    Task DeactivateSeries(Guid id);
}
