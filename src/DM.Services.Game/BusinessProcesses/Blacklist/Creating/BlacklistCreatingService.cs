using System.Linq;
using System.Net;
using System.Threading.Tasks;
using DM.Services.Common.Authorization;
using DM.Services.Core.Dto;
using DM.Services.Core.Dto.Enums;
using DM.Services.Core.Exceptions;
using DM.Services.Game.Authorization;
using DM.Services.Game.BusinessProcesses.Games.Reading;
using DM.Services.Game.BusinessProcesses.Games.Shared;
using DM.Services.Game.Dto.Input;
using DM.Services.MessageQueuing.GeneralBus;
using FluentValidation;

namespace DM.Services.Game.BusinessProcesses.Blacklist.Creating;

/// <inheritdoc />
internal class BlacklistCreatingService : IBlacklistCreatingService
{
    private readonly IValidator<OperateBlacklistLink> _validator;
    private readonly IGameReadingService _gameReadingService;
    private readonly IIntentionManager _intentionManager;
    private readonly IGameBlacklistFactory _factory;
    private readonly IUserRepository _userRepository;
    private readonly IBlacklistCreatingRepository _repository;
    private readonly IInvokedEventProducer _producer;

    /// <inheritdoc />
    public BlacklistCreatingService(
        IValidator<OperateBlacklistLink> validator,
        IGameReadingService gameReadingService,
        IIntentionManager intentionManager,
        IGameBlacklistFactory factory,
        IUserRepository userRepository,
        IBlacklistCreatingRepository repository,
        IInvokedEventProducer producer)
    {
        _validator = validator;
        _gameReadingService = gameReadingService;
        _intentionManager = intentionManager;
        _factory = factory;
        _userRepository = userRepository;
        _repository = repository;
        _producer = producer;
    }

    /// <inheritdoc />
    public async Task<GeneralUser> Create(OperateBlacklistLink operateBlacklistLink)
    {
        await _validator.ValidateAndThrowAsync(operateBlacklistLink);
        var game = await _gameReadingService.GetGame(operateBlacklistLink.GameId);
        _intentionManager.ThrowIfForbidden(GameIntention.Edit, game);

        var (_, userId) = await _userRepository.FindUserId(operateBlacklistLink.Login);
        if (game.BlacklistedUsers.Any(l => l.UserId == userId))
        {
            throw new HttpException(HttpStatusCode.Conflict, "User already blacklisted");
        }

        if (game.Master.UserId == userId || game.Mentor?.UserId == userId)
        {
            throw new HttpException(HttpStatusCode.Forbidden,
                "Game owner and game moderator cannot be blacklisted");
        }

        var blackListLink = _factory.Create(game.Id, userId);
        var blacklistedUser = await _repository.Create(blackListLink);
        await _producer.Send(EventType.ChangedGame, game.Id);

        return blacklistedUser;
    }
}