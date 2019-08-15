using System;
using DM.Services.Core.Dto.Enums;
using DM.Services.Gaming.Dto.Input;
using DbGame = DM.Services.DataAccess.BusinessObjects.Games.Game;

namespace DM.Services.Gaming.BusinessProcesses.Games.Creating
{
    /// <summary>
    /// Factory for game DAL model
    /// </summary>
    public interface IGameFactory
    {
        /// <summary>
        /// Create game and its initial room out of DTO model
        /// </summary>
        /// <param name="createGame">DTO model</param>
        /// <param name="masterId">User identifier</param>
        /// <param name="assistantId">Assistant user identifier</param>
        /// <param name="initialStatus">Initial game status</param>
        /// <returns>Game DAL</returns>
        DbGame Create(CreateGame createGame, Guid masterId, Guid? assistantId, GameStatus initialStatus);
    }
}