using System.Threading.Tasks;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Community.Awards;

/// <summary>
/// API service for the public read of awards
/// </summary>
public interface IAwardApiService
{
    /// <summary>
    /// Get active award types (the catalog)
    /// </summary>
    Task<ListEnvelope<AwardType>> GetTypes();

    /// <summary>
    /// Get active contest series
    /// </summary>
    Task<ListEnvelope<ContestSeries>> GetSeries();

    /// <summary>
    /// Get a user's awards
    /// </summary>
    /// <param name="username">Username</param>
    Task<ListEnvelope<UserAward>> GetUserAwards(string username);
}
