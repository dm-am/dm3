using System;
using System.Collections.Generic;
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

    Task Remove(Guid gameId, Guid userId);
}
