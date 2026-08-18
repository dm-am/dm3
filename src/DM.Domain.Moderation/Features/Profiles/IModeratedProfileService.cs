using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Users;

namespace DM.Domain.Moderation.Features.Profiles;

/// <summary>
/// Service for moderated user profiles.
/// Provides moderation-specific operations on user profiles.
/// </summary>
public interface IModeratedProfileService
{
    /// <summary>
    /// Get user profile for moderation purposes
    /// </summary>
    Task<UserDetails> GetProfile(string username);

    /// <summary>
    /// Moderate user profile (edit Info field)
    /// </summary>
    Task<UserDetails> ModerateProfile(string username, string info);

    /// <summary>
    /// Set user role
    /// </summary>
    Task SetUserRole(string username, UserRole role);

    /// <summary>
    /// Put the user under moderation watch, or take them off it. While the flag
    /// is on, every game and blog the user creates starts in premoderation.
    /// </summary>
    Task<UserDetails> SetModerationWatch(string username, bool underWatch);
}
