using System.Threading.Tasks;

namespace DM.Web.API.Features.Personal.Profiles;

/// <summary>
/// Service for managing user profile (own profile)
/// </summary>
public interface IPersonalProfileApiService
{
    /// <summary>
    /// Get current user's full profile (PersonalProfile)
    /// </summary>
    Task<PersonalProfile> GetMyProfile();

    /// <summary>
    /// Update current user's profile
    /// </summary>
    Task<PersonalProfile> UpdateMyProfile(UpdateProfile profile);

    /// <summary>
    /// Reset the current user's avatar (unlink + GC of the old Upload).
    /// Idempotent: if there is no avatar — no-op.
    /// </summary>
    Task RemoveMyAvatar();
}
