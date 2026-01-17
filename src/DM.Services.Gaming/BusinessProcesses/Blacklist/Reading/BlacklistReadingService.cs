using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Services.Common.Authorization;
using DM.Services.Core.Dto;
using DM.Services.Gaming.Authorization;
using DM.Services.Gaming.BusinessProcesses.Games.Reading;

namespace DM.Services.Gaming.BusinessProcesses.Blacklist.Reading;

/// <inheritdoc />
internal class BlacklistReadingService : IBlacklistReadingService
{
    private readonly IGameReadingService _gameReadingService;
    private readonly IIntentionManager _intentionManager;
    private readonly IBlacklistReadingRepository _repository;

    /// <summary>
    ///
    /// </summary>
    public BlacklistReadingService(
        IGameReadingService gameReadingService,
        IIntentionManager intentionManager,
        IBlacklistReadingRepository repository)
    {
        _gameReadingService = gameReadingService;
        _intentionManager = intentionManager;
        _repository = repository;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<GeneralUser>> Get(Guid gameId)
    {
        var game = await _gameReadingService.GetGame(gameId);
        _intentionManager.ThrowIfForbidden(GameIntention.Edit, game);
        return await _repository.Get(gameId);
    }
}