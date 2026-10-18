using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;

namespace DM.Domain.Personal.Features.Blacklists;

/// <summary>
/// Service for managing user blacklist
/// </summary>
public interface IUserBlacklistService
{
    /// <summary>
    /// Get current user's blacklist
    /// </summary>
    Task<IEnumerable<BlacklistEntry>> GetMyBlacklist(CancellationToken ct = default);

    /// <summary>
    /// Get blacklist settings
    /// </summary>
    Task<UserBlacklistSettings> GetSettings(CancellationToken ct = default);

    /// <summary>
    /// Update blacklist settings
    /// </summary>
    Task<UserBlacklistSettings> UpdateSettings(UserBlacklistSettings settings, CancellationToken ct = default);

    /// <summary>
    /// Block a user
    /// </summary>
    /// <param name="dto">DTO with username to block</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Created blacklist entry</returns>
    Task<BlacklistEntry> Block(OperateUserBlacklistLink dto, CancellationToken ct = default);

    /// <summary>
    /// Unblock a user
    /// </summary>
    /// <param name="dto">DTO with username to unblock</param>
    /// <param name="ct">Cancellation token</param>
    Task Unblock(OperateUserBlacklistLink dto, CancellationToken ct = default);
}
