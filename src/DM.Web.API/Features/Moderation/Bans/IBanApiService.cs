using System;
using System.Threading.Tasks;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Moderation.Bans;

/// <summary>
/// API service for ban management
/// </summary>
public interface IBanApiService
{
    /// <summary>
    /// Get ban status and history for a user
    /// </summary>
    Task<UserBanStatus> GetUserBanStatus(string login);

    /// <summary>
    /// Get active ban for a user (or null)
    /// </summary>
    Task<Envelope<Ban>?> GetActiveBan(string login);

    /// <summary>
    /// Get all active bans (for moderators)
    /// </summary>
    Task<ListEnvelope<Ban>> GetAllActiveBans(BanType? type = null);

    /// <summary>
    /// Create a ban
    /// </summary>
    Task<Envelope<Ban>> CreateBan(CreateBanRequest request);

    /// <summary>
    /// Lift (cancel) a ban early
    /// </summary>
    Task LiftBan(Guid banId, LiftBanRequest? request = null);
}
