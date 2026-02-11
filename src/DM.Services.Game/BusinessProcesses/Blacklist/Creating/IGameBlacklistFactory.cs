using System;
using DM.Services.DataAccess.BusinessObjects.Games.Links;

namespace DM.Services.Game.BusinessProcesses.Blacklist.Creating;

/// <summary>
/// Factory for blacklist link DAL model
/// </summary>
internal interface IGameBlacklistFactory
{
    /// <summary>
    /// Create DAL model
    /// </summary>
    /// <param name="gameId">Game identifier</param>
    /// <param name="userId">User identifier</param>
    /// <returns></returns>
    GameBlacklist Create(Guid gameId, Guid userId);
}