using System.Threading.Tasks;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Moderation.Profiles;

/// <summary>
/// API service for aggregated moderation profile data
/// </summary>
public interface IModerationProfileApiService
{
    /// <summary>
    /// Get aggregated moderation profile for a user.
    /// Fields are filtered by the caller's role:
    /// Admin sees all fields; Moderator sees linked profiles, notes, violations.
    /// </summary>
    /// <param name="login">Target user login</param>
    /// <returns>Moderation profile with role-based field filtering</returns>
    Task<Envelope<ModerationProfileDto>> GetModerationProfile(string login);
}
