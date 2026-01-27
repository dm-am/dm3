using System;
using System.Collections.Generic;
using DM.Services.Core.Dto.Enums;
using DM.Services.DataAccess.BusinessObjects.Games.Links;
using DM.Services.DataAccess.BusinessObjects.Games.Posts;
using DM.Services.Gaming.Dto.Input;
using DbGame = DM.Services.DataAccess.BusinessObjects.Games.Game;

namespace DM.Services.Gaming.BusinessProcesses.Games.Creating.Facades;

/// <summary>
/// Facade for creating game-related entities
/// </summary>
public interface IGameEntityFactory
{
    /// <summary>
    /// Creates a new game entity
    /// </summary>
    DbGame CreateGame(CreateGame createGame, Guid masterId, GameStatus initialStatus,
        PremoderationStatus premoderationStatus, bool isRecruitmentOpen);

    /// <summary>
    /// Creates the default room for a game
    /// </summary>
    Room CreateDefaultRoom(Guid gameId);

    /// <summary>
    /// Creates game tag links for the specified tags
    /// </summary>
    IEnumerable<GameTag> CreateTags(Guid gameId, IEnumerable<Guid> tagIds);
}
