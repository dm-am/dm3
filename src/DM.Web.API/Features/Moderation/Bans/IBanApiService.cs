using System;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Moderation.Bans;

/// <summary>
/// API service for ban management
/// </summary>
public interface IBanApiService
{
    /// <summary>
    /// Get ban status and history for a user (full view,
    /// for moderation profile aggregation)
    /// </summary>
    Task<UserBanStatus> GetUserBanStatus(string login);

    /// <summary>
    /// Get ban status and history for a user (trimmed public view)
    /// </summary>
    Task<PublicUserBanStatus> GetPublicUserBanStatus(string login);

    /// <summary>
    /// Get active ban for a user (or null), trimmed public view
    /// </summary>
    Task<Envelope<PublicBan>?> GetActiveBan(string login);

    /// <summary>
    /// Get all active bans (for moderators)
    /// </summary>
    Task<ListEnvelope<Ban>> GetAllActiveBans(BanType? type = null);

    /// <summary>
    /// Get full ban history - active, expired and lifted - newest first, paged (for moderators)
    /// </summary>
    Task<ListEnvelope<Ban>> GetBanHistory(PagingQuery query);

    /// <summary>
    /// Create a ban
    /// </summary>
    Task<Envelope<Ban>> CreateBan(CreateBanRequest request);

    /// <summary>
    /// Lift (cancel) a ban early
    /// </summary>
    Task LiftBan(Guid banId, LiftBanRequest? request = null);
}
