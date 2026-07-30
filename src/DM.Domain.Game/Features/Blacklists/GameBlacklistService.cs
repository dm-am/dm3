using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
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
using DM.Domain.Core.Subscriptions;
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
    private readonly ISubscriptionRepository _subscriptionRepository;
    private readonly IEventProducer _producer;

    /// <inheritdoc />
    public GameBlacklistService(
        IValidator<OperateBlacklistLink> validator,
        IGameService gameService,
        IIntentionManager intentionManager,
        IIdentityProvider identityProvider,
        IUserLookupService userLookupService,
        IGameBlacklistRepository repository,
        IGameInvitationRepository invitationRepository,
        ICharacterRepository characterRepository,
        ISubscriptionRepository subscriptionRepository,
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
        _subscriptionRepository = subscriptionRepository;
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
        if (game.BlacklistedUsers.Any(b => b.UserId == userId))
        {
            throw new HttpException(HttpStatusCode.Conflict, "Пользователь уже в черном списке");
        }

        if (game.Master.UserId == userId || game.Mentor?.UserId == userId)
        {
            throw new HttpException(HttpStatusCode.Forbidden,
                "Мастера и наставника игры нельзя внести в черный список");
        }

        // Cannot blacklist a member (they must be removed first). Membership is
        // read off the game directly rather than through GetRoles: this is the
        // only question in the codebase asked about somebody other than the
        // current viewer, and GetRoles answers Reader from the viewer-scoped
        // subscriber flag without comparing it to the id it was handed. Calling
        // it here would put the viewer's own readership in the result set — a
        // value nothing may read, which is exactly the kind of thing a later
        // edit reads by accident.
        var isSubscriber = await _subscriptionRepository.FindAsync(
            userId, SubscriptionTargetType.Game, game.Id) != null;
        var isMember = game.Assistants.Any(a => a.UserId == userId) ||
                       game.Players.Any(p => p.UserId == userId) ||
                       isSubscriber;
        if (isMember)
        {
            throw new HttpException(HttpStatusCode.Conflict,
                "Сначала удалите пользователя из игры");
        }

        var currentUserId = _identityProvider.Current.User.UserId;
        var blacklistedUser = await _repository.Add(game.Id, userId, currentUserId);

        // Cancel pending invitations for this user
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

        // Decline pending characters for this user
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
        if (!game.BlacklistedUsers.Any(b => b.UserId == userId))
        {
            throw new HttpException(HttpStatusCode.Conflict, "Пользователя нет в черном списке");
        }

        await _repository.Remove(game.Id, userId);
    }

    #region IContentBlacklistService implementation

    Task<IEnumerable<GeneralUser>> DM.Domain.Core.Blacklists.IContentBlacklistService.GetBlacklistAsync(
        Guid entityId, CancellationToken ct) => Get(entityId);

    async Task<GeneralUser> DM.Domain.Core.Blacklists.IContentBlacklistService.AddToBlacklistAsync(
        Guid entityId, string username, CancellationToken ct)
    {
        return await Add(new OperateBlacklistLink { GameId = entityId, Username = username });
    }

    async Task DM.Domain.Core.Blacklists.IContentBlacklistService.RemoveFromBlacklistAsync(
        Guid entityId, string username, CancellationToken ct)
    {
        await Remove(new OperateBlacklistLink { GameId = entityId, Username = username });
    }

    public async Task<bool> IsBlockedAsync(Guid gameId, Guid userId, CancellationToken ct = default)
    {
        return await _repository.IsBlocked(gameId, userId, ct);
    }

    #endregion
}
