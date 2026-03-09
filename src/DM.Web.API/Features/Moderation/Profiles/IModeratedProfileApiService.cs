using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Web.API.Features.Community.Users;

namespace DM.Web.API.Features.Moderation.Profiles;

/// <summary>
/// API service for moderated user profiles
/// </summary>
public interface IModeratedProfileApiService
{
    /// <summary>
    /// Get aggregated moderation profile for a user.
    /// Fields are filtered by the caller's role:
    /// Admin sees all fields; Moderator sees linked profiles, notes, violations.
    /// </summary>
    /// <param name="username">Target username</param>
    /// <returns>Moderated profile with role-based field filtering</returns>
    Task<ModeratedProfile> GetModeratedProfile(string username);

    /// <summary>
    /// Moderate user profile (SeniorModerator+ only)
    /// </summary>
    /// <param name="username">User's display name</param>
    /// <param name="profile">Moderation data</param>
    /// <returns>Updated user profile</returns>
    Task<UserProfile> ModerateUserProfile(string username, ModerateProfile profile);

    /// <summary>
    /// Set user role (Admin only)
    /// </summary>
    /// <param name="username">User's display name</param>
    /// <param name="role">New role</param>
    /// <returns>Updated user profile</returns>
    Task<UserProfile> SetUserRole(string username, UserRole role);
}
