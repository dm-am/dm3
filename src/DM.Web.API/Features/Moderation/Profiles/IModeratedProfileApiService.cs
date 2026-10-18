using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Shared.Dto;

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
    /// Put the user under moderation watch, or take them off it (Moderator+ only)
    /// </summary>
    /// <param name="username">User's display name</param>
    /// <param name="underWatch">Whether the watch is on from now on</param>
    /// <returns>
    /// The moderated profile, re-read after the change, in the envelope every
    /// single-resource body outside the inherited legacy list carries.
    /// </returns>
    Task<Envelope<ModeratedProfile>> SetModerationWatch(string username, bool underWatch);

    /// <summary>
    /// Set user role (Admin only)
    /// </summary>
    /// <param name="username">User's display name</param>
    /// <param name="role">New role</param>
    /// <returns>Updated user profile</returns>
    Task<UserProfile> SetUserRole(string username, UserRole role);

    /// <summary>
    /// Remove a user's account on a moderator's decision (SeniorModerator+ only)
    /// </summary>
    /// <param name="username">User's display name</param>
    /// <param name="request">Why the account is being removed</param>
    Task RemoveAccount(string username, RemoveAccountRequest request);

    /// <summary>
    /// Bring a removed account back on a moderator's decision (SeniorModerator+ only)
    /// </summary>
    /// <param name="username">Whose account to bring back</param>
    Task RestoreAccount(string username);

    /// <summary>
    /// List the accounts moderation has removed (SeniorModerator+ only)
    /// </summary>
    /// <param name="query">Search term and page bounds</param>
    /// <returns>Page of removed accounts, addresses included only for an administrator</returns>
    Task<ListEnvelope<RemovedAccount>> GetRemovedAccounts(RemovedAccountsQuery query);
}
