using System;
using System.Threading.Tasks;
using DM.Services.Common.Authorization;
using DM.Services.Core.Dto.Enums;
using DM.Services.DataAccess.RelationalStorage;
using DM.Services.Game.Authorization;
using DM.Services.Game.BusinessProcesses.Games.Reading;
using DM.Services.Game.BusinessProcesses.Games.Updating;
using DM.Services.MessageQueuing.GeneralBus;
using DbGame = DM.Services.DataAccess.BusinessObjects.Games.Game;

namespace DM.Services.Game.BusinessProcesses.Games.Deleting;

/// <inheritdoc />
internal class GameDeletingService : IGameDeletingService
{
    private readonly IGameReadingService _gameReadingService;
    private readonly IIntentionManager _intentionManager;
    private readonly IUpdateBuilderFactory _updateBuilderFactory;
    private readonly IGameUpdatingRepository _repository;
    private readonly IInvokedEventProducer _producer;

    /// <inheritdoc />
    public GameDeletingService(
        IGameReadingService gameReadingService,
        IIntentionManager intentionManager,
        IUpdateBuilderFactory updateBuilderFactory,
        IGameUpdatingRepository repository,
        IInvokedEventProducer producer)
    {
        _gameReadingService = gameReadingService;
        _intentionManager = intentionManager;
        _updateBuilderFactory = updateBuilderFactory;
        _repository = repository;
        _producer = producer;
    }

    /// <inheritdoc />
    public async Task DeleteGame(Guid gameId)
    {
        var gameToRemove = await _gameReadingService.GetGame(gameId);
        _intentionManager.ThrowIfForbidden(GameIntention.Delete, gameToRemove);

        var updateBuilder = _updateBuilderFactory.Create<DbGame>(gameId)
            .Field(g => g.IsRemoved, true);
        await _repository.Update(updateBuilder);
        await _producer.Send(EventType.DeletedGame, gameId);
    }
}