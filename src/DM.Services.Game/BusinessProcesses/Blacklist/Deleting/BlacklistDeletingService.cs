using System.Linq;
using System.Net;
using System.Threading.Tasks;
using DM.Services.Common.Authorization;
using DM.Services.Core.Dto.Enums;
using DM.Services.Core.Exceptions;
using DM.Services.DataAccess.BusinessObjects.Games.Links;
using DM.Services.DataAccess.RelationalStorage;
using DM.Services.Game.Authorization;
using DM.Services.Game.BusinessProcesses.Games.Reading;
using DM.Services.Game.BusinessProcesses.Games.Shared;
using DM.Services.Game.Dto.Input;
using DM.Services.MessageQueuing.GeneralBus;
using FluentValidation;

namespace DM.Services.Game.BusinessProcesses.Blacklist.Deleting;

/// <inheritdoc />
internal class BlacklistDeletingService : IBlacklistDeletingService
{
    private readonly IValidator<OperateBlacklistLink> _validator;
    private readonly IUserRepository _userRepository;
    private readonly IGameReadingService _gameReadingService;
    private readonly IIntentionManager _intentionManager;
    private readonly IUpdateBuilderFactory _updateBuilderFactory;
    private readonly IBlacklistDeletingRepository _repository;
    private readonly IInvokedEventProducer _invokedEventProducer;

    /// <inheritdoc />
    public BlacklistDeletingService(
        IValidator<OperateBlacklistLink> validator,
        IUserRepository userRepository,
        IGameReadingService gameReadingService,
        IIntentionManager intentionManager,
        IUpdateBuilderFactory updateBuilderFactory,
        IBlacklistDeletingRepository repository,
        IInvokedEventProducer invokedEventProducer)
    {
        _validator = validator;
        _userRepository = userRepository;
        _gameReadingService = gameReadingService;
        _intentionManager = intentionManager;
        _updateBuilderFactory = updateBuilderFactory;
        _repository = repository;
        _invokedEventProducer = invokedEventProducer;
    }

    /// <inheritdoc />
    public async Task Delete(OperateBlacklistLink operateBlacklistLink)
    {
        await _validator.ValidateAndThrowAsync(operateBlacklistLink);
        var game = await _gameReadingService.GetGame(operateBlacklistLink.GameId);
        _intentionManager.ThrowIfForbidden(GameIntention.Edit, game);

        var (_, userId) = await _userRepository.FindUserId(operateBlacklistLink.Login);
        var blacklistedLink = game.BlacklistedUsers.FirstOrDefault(u => u.UserId == userId);
        if (blacklistedLink == default)
        {
            throw new HttpException(HttpStatusCode.Conflict, "User is not blacklisted");
        }

        var updateBuilder = _updateBuilderFactory.Create<GameBlacklist>(blacklistedLink.LinkId).Delete();
        await _repository.Delete(updateBuilder);
        await _invokedEventProducer.Send(EventType.ChangedGame, game.Id);
    }
}