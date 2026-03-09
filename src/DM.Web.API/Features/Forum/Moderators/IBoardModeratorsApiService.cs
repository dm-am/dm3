using System.Threading.Tasks;
using DM.Web.API.Shared.Dto;
using DM.Web.API.Features.Community.Users;

namespace DM.Web.API.Features.Forum.Moderators;

/// <summary>
/// API service for forum moderators
/// </summary>
public interface IBoardModeratorsApiService
{
    /// <summary>
    /// Get list of forum moderators
    /// </summary>
    /// <param name="id">Forum id</param>
    /// <returns>Envelope of moderators list</returns>
    Task<ListEnvelope<User>> GetModerators(string id);
}
