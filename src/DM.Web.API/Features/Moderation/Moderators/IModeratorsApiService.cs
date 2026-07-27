using System.Threading.Tasks;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Moderation.Moderators;

/// <summary>
/// API service for the moderation team overview
/// </summary>
public interface IModeratorsApiService
{
    /// <summary>
    /// Get all users with Moderator role or higher, each with their zones of
    /// responsibility: assigned forum boards and curated games/blogs
    /// </summary>
    /// <returns>Envelope of moderator overviews</returns>
    Task<ListEnvelope<ModeratorOverview>> GetModerators();
}
