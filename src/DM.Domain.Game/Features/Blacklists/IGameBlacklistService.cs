using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Blacklists;
using DM.Domain.Core.Dto;


namespace DM.Domain.Game.Features.Blacklists;
/// <summary>
/// Service for game blacklist management
/// </summary>
public interface IGameBlacklistService : IContentBlacklistService
{
    /// <summary>
    /// Get list of blacklisted users for a game
    /// </summary>
    /// <param name="gameId">Game identifier</param>
    /// <returns>List of blacklisted users</returns>
    Task<IEnumerable<GeneralUser>> Get(Guid gameId);
    /// Add user to game blacklist
    /// <param name="operateBlacklistLink">DTO with game ID and username</param>
    /// <returns>Blacklisted user</returns>
    Task<GeneralUser> Add(OperateBlacklistLink operateBlacklistLink);
    /// Remove user from game blacklist
    Task Remove(OperateBlacklistLink operateBlacklistLink);
}
