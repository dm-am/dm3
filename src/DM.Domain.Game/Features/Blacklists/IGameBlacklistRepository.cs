using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;

namespace DM.Domain.Game.Features.Blacklists;

/// <summary>
/// Repository for game blacklist operations
/// </summary>
public interface IGameBlacklistRepository
{
    /// <summary>
    /// Get all blacklisted users for a game
    /// </summary>
    Task<IEnumerable<GeneralUser>> Get(Guid gameId);

    /// <summary>
    /// Add user to game blacklist
    /// </summary>
    Task<GeneralUser> Add(Guid gameId, Guid blockedUserId, Guid blockedByUserId);

    /// <summary>
    /// Remove user from game blacklist
    /// </summary>
    Task Remove(Guid gameId, Guid userId);

    /// <summary>
    /// Check if user is blacklisted in game
    /// </summary>
    Task<bool> IsBlocked(Guid gameId, Guid userId, CancellationToken ct = default);
}
