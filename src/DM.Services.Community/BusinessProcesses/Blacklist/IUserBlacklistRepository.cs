using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Services.Core.Dto.Enums;
using DM.Services.DataAccess.BusinessObjects.Users;

namespace DM.Services.Community.BusinessProcesses.Blacklist;

/// <summary>
/// Repository for user blacklist data access
/// </summary>
public interface IUserBlacklistRepository
{
    /// <summary>
    /// Get all blacklist entries for a user
    /// </summary>
    Task<IEnumerable<UserBlacklist>> GetBlacklist(Guid ownerId, CancellationToken ct = default);

    /// <summary>
    /// Get blacklist entry by ID
    /// </summary>
    Task<UserBlacklist?> Get(Guid entryId, CancellationToken ct = default);

    /// <summary>
    /// Find existing blacklist entry
    /// </summary>
    Task<UserBlacklist?> Find(Guid ownerId, Guid blockedUserId, CancellationToken ct = default);

    /// <summary>
    /// Check if a user is blocked by another user
    /// </summary>
    Task<bool> IsBlocked(Guid ownerId, Guid blockedUserId, CancellationToken ct = default);

    /// <summary>
    /// Check if either user has blocked the other (bidirectional check)
    /// </summary>
    Task<bool> HasBlockRelationship(Guid userId1, Guid userId2, CancellationToken ct = default);

    /// <summary>
    /// Create blacklist entry
    /// </summary>
    Task<UserBlacklist> Create(UserBlacklist entry, CancellationToken ct = default);

    /// <summary>
    /// Delete blacklist entry
    /// </summary>
    Task Delete(Guid entryId, CancellationToken ct = default);

    /// <summary>
    /// Get IDs of users blocked by a specific user
    /// </summary>
    Task<IEnumerable<Guid>> GetBlockedUserIds(Guid ownerId, CancellationToken ct = default);

    /// <summary>
    /// Get blacklist settings for a user
    /// </summary>
    Task<UserBlacklistSettings> GetSettings(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Update blacklist settings for a user
    /// </summary>
    Task<UserBlacklistSettings> UpdateSettings(Guid userId, UserBlacklistSettings settings, CancellationToken ct = default);
}
