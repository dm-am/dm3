using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Users;

namespace DM.Domain.Moderation.Features.Profiles;

/// <summary>
/// Moderation-specific operations on user profiles
/// </summary>
public interface IModeratedProfileService
{
    Task<UserDetails> GetProfile(string username);

    /// <summary>
    /// Edit the Info field of the profile
    /// </summary>
    Task<UserDetails> ModerateProfile(string username, string info);

    Task SetUserRole(string username, UserRole role);

    /// <summary>
    /// Put the user under moderation watch, or take them off it. While the flag
    /// is on, every game and blog the user creates starts in premoderation.
    /// </summary>
    Task<UserDetails> SetModerationWatch(string username, bool underWatch);
}
