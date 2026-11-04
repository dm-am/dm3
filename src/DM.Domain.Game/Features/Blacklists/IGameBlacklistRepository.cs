using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Exceptions;

namespace DM.Domain.Game.Features.Blacklists;

/// <summary>
/// Repository for game blacklist operations
/// </summary>
public interface IGameBlacklistRepository
{
    Task<IEnumerable<GeneralUser>> Get(Guid gameId);

    /// <remarks>
    /// Throws <see cref="DuplicateEntityException" /> when two requests both pass the caller's check.
    /// </remarks>
    Task<GeneralUser> Add(Guid gameId, Guid blockedUserId, Guid blockedByUserId);

    /// <summary>
    /// Copy the owner's personal blacklist into the game's, skipping anyone
    /// already on it
    /// </summary>
    /// <returns>How many entries were added</returns>
    Task<int> CopyFromPersonalBlacklist(Guid gameId, Guid ownerId, CancellationToken ct = default);

    Task Remove(Guid gameId, Guid userId);
}
