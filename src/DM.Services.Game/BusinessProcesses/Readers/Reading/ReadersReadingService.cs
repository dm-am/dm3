using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Core.Dto;
using DM.Services.Game.BusinessProcesses.Games.Reading;

namespace DM.Services.Game.BusinessProcesses.Readers.Reading;

/// <inheritdoc />
internal class ReadersReadingService : IReadersReadingService
{
    private readonly IGameReadingService _gameReadingService;
    private readonly IReadersReadingRepository _repository;
    private readonly IIdentityProvider _identityProvider;

    /// <inheritdoc />
    public ReadersReadingService(
        IGameReadingService gameReadingService,
        IReadersReadingRepository repository,
        IIdentityProvider identityProvider)
    {
        _gameReadingService = gameReadingService;
        _repository = repository;
        _identityProvider = identityProvider;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<GeneralUser>> Get(Guid gameId)
    {
        await _gameReadingService.GetGame(gameId);
        return await _repository.Get(gameId, _identityProvider.Current.User.UserId);
    }
}