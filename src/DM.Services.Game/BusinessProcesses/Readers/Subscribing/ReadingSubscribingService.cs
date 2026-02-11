using System;
using System.Net;
using System.Threading.Tasks;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Common.Authorization;
using DM.Services.Core.Dto;
using DM.Services.Core.Exceptions;
using DM.Services.Game.Authorization;
using DM.Services.Game.BusinessProcesses.Games.Reading;

namespace DM.Services.Game.BusinessProcesses.Readers.Subscribing;

/// <inheritdoc />
internal class ReadingSubscribingService : IReadingSubscribingService
{
    private readonly IGameReadingService _gameReadingService;
    private readonly IReaderFactory _readerFactory;
    private readonly IReadingSubscribingRepository _repository;
    private readonly IIntentionManager _intentionManager;
    private readonly IIdentityProvider _identityProvider;

    /// <inheritdoc />
    public ReadingSubscribingService(
        IIdentityProvider identityProvider,
        IGameReadingService gameReadingService,
        IReaderFactory readerFactory,
        IReadingSubscribingRepository repository,
        IIntentionManager intentionManager)
    {
        _gameReadingService = gameReadingService;
        _readerFactory = readerFactory;
        _repository = repository;
        _intentionManager = intentionManager;
        _identityProvider = identityProvider;
    }
        
    /// <inheritdoc />
    public async Task<GeneralUser> Subscribe(Guid gameId)
    {
        _intentionManager.ThrowIfForbidden(GameIntention.Subscribe);
        var game = await _gameReadingService.GetGame(gameId);
        _intentionManager.ThrowIfForbidden(GameIntention.Subscribe, game);

        var identity = _identityProvider.Current;
        var userId = identity.User.UserId;
        if (await _repository.HasSubscription(userId, gameId))
        {
            throw new HttpException(HttpStatusCode.Conflict, "User already subscribed to this game");
        }

        var reader = _readerFactory.Create(userId, gameId);
        await _repository.Add(reader);
        return identity.User;
    }

    /// <inheritdoc />
    public async Task Unsubscribe(Guid gameId)
    {
        _intentionManager.ThrowIfForbidden(GameIntention.Subscribe);
        var game = await _gameReadingService.GetGame(gameId);
        _intentionManager.ThrowIfForbidden(GameIntention.Unsubscribe, game);

        var userId = _identityProvider.Current.User.UserId;
        if (!await _repository.HasSubscription(userId, gameId))
        {
            throw new HttpException(HttpStatusCode.Conflict, "User is not subscribed to this game");
        }

        await _repository.Delete(userId, gameId);
    }
}