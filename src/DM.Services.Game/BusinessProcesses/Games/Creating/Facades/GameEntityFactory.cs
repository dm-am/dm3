using System;
using System.Collections.Generic;
using System.Linq;
using DM.Services.Core.Dto.Enums;
using DM.Services.DataAccess.BusinessObjects.Games.Links;
using DM.Services.DataAccess.BusinessObjects.Games.Posts;
using DM.Services.Game.BusinessProcesses.Games.Shared;
using DM.Services.Game.Dto.Input;
using DbGame = DM.Services.DataAccess.BusinessObjects.Games.Game;

namespace DM.Services.Game.BusinessProcesses.Games.Creating.Facades;

/// <inheritdoc />
internal class GameEntityFactory : IGameEntityFactory
{
    private readonly IGameFactory _gameFactory;
    private readonly IRoomFactory _roomFactory;
    private readonly IGameTagFactory _gameTagFactory;

    public GameEntityFactory(
        IGameFactory gameFactory,
        IRoomFactory roomFactory,
        IGameTagFactory gameTagFactory)
    {
        _gameFactory = gameFactory;
        _roomFactory = roomFactory;
        _gameTagFactory = gameTagFactory;
    }

    /// <inheritdoc />
    public DbGame CreateGame(CreateGame createGame, Guid masterId, ModuleStatus initialStatus,
        PremoderationStatus premoderationStatus, bool isRecruitmentOpen)
    {
        return _gameFactory.Create(createGame, masterId, initialStatus, premoderationStatus, isRecruitmentOpen);
    }

    /// <inheritdoc />
    public Room CreateDefaultRoom(Guid gameId)
    {
        return _roomFactory.Create(gameId);
    }

    /// <inheritdoc />
    public IEnumerable<GameTag> CreateTags(Guid gameId, IEnumerable<Guid> tagIds)
    {
        return tagIds.Select(tagId => _gameTagFactory.Create(gameId, tagId));
    }
}
