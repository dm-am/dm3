using System.Threading.Tasks;
using DM.Services.Core.Dto.Enums;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Dto.Users;

namespace DM.Web.API.Services.Users;

/// <summary>
/// API service for community users
/// </summary>
public interface IUserApiService
{
    /// <summary>
    /// Get community users
    /// </summary>
    /// <param name="query">Paging query</param>
    /// <returns></returns>
    Task<ListEnvelope<User>> GetUsers(UsersQuery query);

    /// <summary>
    /// Get community user by login
    /// </summary>
    /// <param name="login">User login</param>
    /// <returns></returns>
    Task<Envelope<User>> GetUser(string login);

    /// <summary>
    /// Get community user details by login
    /// </summary>
    /// <param name="login"></param>
    /// <returns></returns>
    Task<Envelope<UserDetails>> GetUserDetails(string login);

    /// <summary>
    /// Get users by role
    /// </summary>
    /// <param name="role">User role</param>
    /// <returns></returns>
    Task<ListEnvelope<User>> GetUsersByRole(UserRole role);

    /// <summary>
    /// Update current user profile
    /// </summary>
    /// <param name="profile">Profile update data</param>
    /// <returns>Updated user details</returns>
    Task<Envelope<UserDetails>> UpdateCurrentUserProfile(UpdateProfile profile);

    /// <summary>
    /// Get user settings
    /// </summary>
    /// <param name="login">User login</param>
    /// <returns></returns>
    Task<Envelope<UserSettings>> GetUserSettings(string login);

    /// <summary>
    /// Update user settings
    /// </summary>
    /// <param name="login">User login</param>
    /// <param name="settings">Settings to update</param>
    /// <returns></returns>
    Task<Envelope<UserSettings>> UpdateUserSettings(string login, UserSettings settings);

    /// <summary>
    /// Get best post (highest rated) by user from open rooms
    /// </summary>
    /// <param name="login">User login</param>
    /// <returns>Best post or null if no posts with positive rating found</returns>
    Task<Envelope<BestPost>> GetBestPost(string login);
}