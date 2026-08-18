using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Blacklists;
using DM.Domain.Core.Enums;

namespace DM.Domain.Personal.Features.Blacklists;

/// <summary>
/// Repository for user blacklist data access.
/// Extends IUserBlacklistChecker with full CRUD operations.
/// </summary>
public interface IUserBlacklistRepository : IUserBlacklistChecker
{
    /// <summary>
    /// Get all blacklist entries for a user
    /// </summary>
    Task<IEnumerable<BlacklistEntry>> GetBlacklist(Guid ownerId, CancellationToken ct = default);

    /// <summary>
    /// Get blacklist entry by ID
    /// </summary>
    Task<BlacklistEntry?> Get(Guid entryId, CancellationToken ct = default);

    /// <summary>
    /// Find existing blacklist entry
    /// </summary>
    Task<BlacklistEntry?> Find(Guid ownerId, Guid blockedUserId, CancellationToken ct = default);

    /// <summary>
    /// Create blacklist entry
    /// </summary>
    Task<BlacklistEntry> Create(CreateBlacklistEntryEntity entry, CancellationToken ct = default);

    /// <summary>
    /// Delete blacklist entry
    /// </summary>
    Task Delete(Guid entryId, CancellationToken ct = default);

    /// <summary>
    /// Get blacklist settings for a user
    /// </summary>
    Task<UserBlacklistSettings> GetSettings(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Update blacklist settings for a user
    /// </summary>
    Task<UserBlacklistSettings> UpdateSettings(Guid userId, UserBlacklistSettings settings, CancellationToken ct = default);
}
