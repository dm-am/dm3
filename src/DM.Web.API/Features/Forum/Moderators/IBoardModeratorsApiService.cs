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

    /// <summary>
    /// Add a user as board moderator
    /// </summary>
    /// <param name="id">Board id</param>
    /// <param name="username">Username to add</param>
    /// <returns>Envelope with added user</returns>
    Task<Envelope<User>> AddModerator(string id, string username);

    /// <summary>
    /// Remove a user from board moderators
    /// </summary>
    /// <param name="id">Board id</param>
    /// <param name="username">Username to remove</param>
    /// <returns>Task</returns>
    Task RemoveModerator(string id, string username);
}
