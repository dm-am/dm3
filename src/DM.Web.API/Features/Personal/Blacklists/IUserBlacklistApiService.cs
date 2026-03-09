using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Personal.Blacklists;

/// <summary>
/// API service for user blacklist management
/// </summary>
public interface IUserBlacklistApiService
{
    /// <summary>
    /// Get current user's blacklist
    /// </summary>
    /// <param name="query">Pagination parameters</param>
    Task<(IEnumerable<BlacklistEntry> Entries, PagingInfo Paging)> GetMyBlacklist(PagingQuery query);

    /// <summary>
    /// Get blacklist settings
    /// </summary>
    Task<BlacklistSettings> GetSettings();

    /// <summary>
    /// Update blacklist settings
    /// </summary>
    Task<BlacklistSettings> UpdateSettings(BlacklistSettings settings);

    /// <summary>
    /// Block a user
    /// </summary>
    Task<BlacklistEntry> BlockUser(BlockUserRequest request);

    /// <summary>
    /// Unblock a user
    /// </summary>
    Task UnblockUser(string username);

    /// <summary>
    /// Check if current user can send message to another user
    /// </summary>
    /// <param name="username">Target username</param>
    /// <returns>True if messaging is allowed</returns>
    Task<bool> CanMessage(string username);
}
