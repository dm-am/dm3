using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Game.Features.Games;
using DM.Domain.Core.Users;
using DM.Domain.Game.Authorization;
using DM.Domain.Game.Features.Invitations;
using DM.Domain.Game.Features.Characters;
using DM.Domain.Core.Events;
using FluentValidation;

namespace DM.Domain.Game.Features.Blacklists;

/// <inheritdoc />
internal class GameBlacklistService : IGameBlacklistService
{
    private readonly IValidator<OperateBlacklistLink> _validator;
    private readonly IGameService _gameService;
    private readonly IIntentionManager _intentionManager;
    private readonly IIdentityProvider _identityProvider;
    private readonly IUserLookupService _userLookupService;
    private readonly IGameBlacklistRepository _repository;
    private readonly IGameInvitationRepository _invitationRepository;
    private readonly ICharacterRepository _characterRepository;
    private readonly IEventProducer _producer;

    public GameBlacklistService(
        IValidator<OperateBlacklistLink> validator,
        IGameService gameService,
        IIntentionManager intentionManager,
        IIdentityProvider identityProvider,
        IUserLookupService userLookupService,
        IGameBlacklistRepository repository,
        IGameInvitationRepository invitationRepository,
        ICharacterRepository characterRepository,
        IEventProducer producer)
    {
        _validator = validator;
        _gameService = gameService;
        _intentionManager = intentionManager;
        _identityProvider = identityProvider;
        _userLookupService = userLookupService;
        _repository = repository;
        _invitationRepository = invitationRepository;
        _characterRepository = characterRepository;
        _producer = producer;
    }

    public async Task<IEnumerable<GeneralUser>> Get(Guid gameId)
    {
        var game = await _gameService.GetAsync(gameId);
        _intentionManager.ThrowIfForbidden(GameIntention.Edit, game);
        return await _repository.Get(gameId);
    }

    public async Task<GeneralUser> Add(OperateBlacklistLink operateBlacklistLink)
    {
        await _validator.ValidateAndThrowAsync(operateBlacklistLink);
        var game = await _gameService.GetAsync(operateBlacklistLink.GameId);
        _intentionManager.ThrowIfForbidden(GameIntention.Edit, game);

        var (_, userId) = await _userLookupService.FindUserIdAsync(operateBlacklistLink.Username);
        if (game.IsBlacklisted(userId))
        {
            throw new HttpException(HttpStatusCode.Conflict, RefusalMessage.AlreadyInBlacklist);
        }

        if (game.Master?.UserId == userId || game.Mentor?.UserId == userId)
        {
            throw new HttpException(HttpStatusCode.Forbidden, RefusalMessage.CannotBlacklistGameLead);
        }

        // A member leaves the game first, the list is for people outside it; a subscriber is not a member.
        // Read off the game, not GetRoles: GetRoles answers Reader for the viewer, not for this id.
        var isMember = game.Assistants.Any(a => a.UserId == userId) ||
                       game.Players.Any(p => p.UserId == userId);
        if (isMember)
        {
            throw new HttpException(HttpStatusCode.Conflict, RefusalMessage.RemoveFromGameFirst);
        }

        var currentUserId = _identityProvider.Current.User.UserId;
        GeneralUser blacklistedUser;
        try
        {
            blacklistedUser = await _repository.Add(game.Id, userId, currentUserId);
        }
        catch (DuplicateEntityException)
        {
            // Lost the race against an identical request: the person is on the list either way.
            throw new HttpException(HttpStatusCode.Conflict, RefusalMessage.AlreadyInBlacklist);
        }

        var cancelledInvitations = await _invitationRepository.CancelInvitationsForUser(game.Id, userId);
        foreach (var invitation in cancelledInvitations)
        {
            var eventType = invitation.TokenType switch
            {
                TokenType.GamePlayerInvitation => EventType.PlayerInvitationCancelled,
                TokenType.GameReaderInvitation => EventType.ReaderInvitationCancelled,
                TokenType.GameAssistantInvitation => EventType.AssignmentRequestCancelled,
                _ => EventType.Unknown
            };
            if (eventType != EventType.Unknown)
            {
                await _producer.SendAsync(eventType, invitation.TokenId);
            }
        }

        await _characterRepository.DeclinePendingCharacters(game.Id, userId);

        await _producer.SendAsync(EventType.ChangedGame, game.Id);
        return blacklistedUser;
    }

    public async Task Remove(OperateBlacklistLink operateBlacklistLink)
    {
        await _validator.ValidateAndThrowAsync(operateBlacklistLink);
        var game = await _gameService.GetAsync(operateBlacklistLink.GameId);
        _intentionManager.ThrowIfForbidden(GameIntention.Edit, game);

        var (_, userId) = await _userLookupService.FindUserIdAsync(operateBlacklistLink.Username);
        if (!game.IsBlacklisted(userId))
        {
            throw new HttpException(HttpStatusCode.Conflict, RefusalMessage.NotInBlacklist);
        }

        await _repository.Remove(game.Id, userId);
    }
}
