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

    /// <summary>
    /// Check if a user is blocked by current user
    /// </summary>
    Task<bool> IsBlocked(string username, CancellationToken ct = default);

    /// <summary>
    /// Check if current user can send message to another user
    /// </summary>
    Task<bool> CanSendMessage(Guid targetUserId, CancellationToken ct = default);

    /// <summary>
    /// Get detailed block status between current user and target user
    /// </summary>
    Task<BlockStatus> GetBlockStatus(Guid targetUserId, CancellationToken ct = default);
}

/// <summary>
/// Detailed block status between two users
/// </summary>
public class BlockStatus
{
    /// <summary>
    /// Whether conversation can proceed
    /// </summary>
    public bool CanCommunicate { get; set; }

    /// <summary>
    /// Block reason (null if can communicate)
    /// </summary>
    /// <remarks>
    /// Possible values:
    /// - YouBlockedThem: You have blocked the target user
    /// - CannotCommunicate: Communication is not possible (privacy-protected)
    /// </remarks>
    public string? Reason { get; set; }
}
