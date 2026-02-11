using System;
using DM.Services.Core.Dto.Enums;
using DM.Services.Game.Dto.Input;
using DbGame = DM.Services.DataAccess.BusinessObjects.Games.Game;

namespace DM.Services.Game.BusinessProcesses.Games.Creating;

/// <summary>
/// Factory for game DAL model
/// </summary>
internal interface IGameFactory
{
    /// <summary>
    /// Create game and its initial room out of DTO model
    /// </summary>
    /// <param name="createGame">DTO model</param>
    /// <param name="masterId">User identifier</param>
    /// <param name="initialStatus">Initial game status</param>
    /// <param name="premoderationStatus">Premoderation status</param>
    /// <param name="isRecruitmentOpen">Whether recruitment is open</param>
    /// <returns>Game DAL</returns>
    DbGame Create(CreateGame createGame, Guid masterId, ModuleStatus initialStatus,
        PremoderationStatus premoderationStatus, bool isRecruitmentOpen);
}