using System.Threading.Tasks;
using DM.Web.API.Dto.Community;
using DM.Web.API.Dto.Contracts;

namespace DM.Web.API.Services.Community;

/// <summary>
/// Service for community statistics
/// </summary>
public interface ICommunityStatsService
{
    /// <summary>
    /// Get community statistics
    /// </summary>
    /// <returns>Community statistics</returns>
    Task<Envelope<CommunityStats>> GetStats();
}
