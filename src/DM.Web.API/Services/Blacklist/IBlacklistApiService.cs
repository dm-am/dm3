using System.Threading.Tasks;
using DM.Services.Core.Dto.Enums;
using DM.Web.API.Dto.Blacklist;
using DM.Web.API.Dto.Contracts;

namespace DM.Web.API.Services.Blacklist;

/// <summary>
/// API service for user blacklist management
/// </summary>
public interface IBlacklistApiService
{
    /// <summary>
    /// Get current user's blacklist
    /// </summary>
    Task<ListEnvelope<BlacklistEntry>> GetMyBlacklist();

    /// <summary>
    /// Get blacklist settings
    /// </summary>
    Task<Envelope<UserBlacklistSettings>> GetSettings();

    /// <summary>
    /// Update blacklist settings
    /// </summary>
    Task<Envelope<UserBlacklistSettings>> UpdateSettings(UserBlacklistSettings settings);

    /// <summary>
    /// Block a user
    /// </summary>
    Task<Envelope<BlacklistEntry>> BlockUser(BlockUserRequest request);

    /// <summary>
    /// Unblock a user
    /// </summary>
    Task UnblockUser(string login);
}
