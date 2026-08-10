using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Users;
using DM.Domain.Game.Authorization;
using DM.Domain.Game.Features.Games;
using DM.Domain.Core.Blacklists;
using DM.Domain.Game.Features.Subscriptions;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Events;
using GameDto = DM.Domain.Game.Features.Games.Game;

namespace DM.Domain.Game.Features.Invitations;

/// <inheritdoc />
internal class GameInvitationService : IGameInvitationService
{
    private const int InvitationExpirationDays = 30;

    private readonly IIdentityProvider _identityProvider;
    private readonly IIntentionManager _intentionManager;
    private readonly IGameRepository _gameRepository;
    private readonly IUserLookupService _userLookupService;
    private readonly IGameInvitationRepository _repository;
    private readonly IGameSubscriptionService _subscriptionService;
    private readonly IUserBlacklistChecker _userBlacklistChecker;
    private readonly IEventProducer _producer;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IGuidFactory _guidFactory;

    public GameInvitationService(
        IIdentityProvider identityProvider,
        IIntentionManager intentionManager,
        IGameRepository gameRepository,
        IUserLookupService userLookupService,
        IGameInvitationRepository repository,
        IGameSubscriptionService subscriptionService,
        IUserBlacklistChecker userBlacklistChecker,
        IEventProducer producer,
        IDateTimeProvider dateTimeProvider,
        IGuidFactory guidFactory)
    {
        _identityProvider = identityProvider;
        _intentionManager = intentionManager;
        _gameRepository = gameRepository;
        _userLookupService = userLookupService;
        _repository = repository;
        _subscriptionService = subscriptionService;
        _userBlacklistChecker = userBlacklistChecker;
        _producer = producer;
        _dateTimeProvider = dateTimeProvider;
        _guidFactory = guidFactory;
    }

    #region Invitations

    /// <inheritdoc />
    public async Task<GameInvitation> InvitePlayer(Guid gameId, string username, CancellationToken ct = default)
    {
        var game = await GetGameOrThrow(gameId);
        _intentionManager.ThrowIfForbidden(GameIntention.InvitePlayer, game);

        var user = await _userLookupService.GetAsync(username);
        await ValidateInvitation(game, user.UserId, ct);

        var entity = new CreateGameInvitationEntity
        {
            TokenId = _guidFactory.Create(),
            GameId = gameId,
            UserId = user.UserId,
            CreatorId = _identityProvider.Current.User.UserId,
            TokenType = TokenType.GamePlayerInvitation,
            CreatedUtc = _dateTimeProvider.Now
        };

        var token = await _repository.InvalidateAndCreateInvitation(entity, ct);
        await _producer.SendAsync(EventType.PlayerInvitationCreated, token.TokenId);

        return await GetInvitationInfo(token.TokenId, ct);
    }

    public async Task<GameInvitation> InviteReader(Guid gameId, string username, CancellationToken ct = default)
    {
        var game = await GetGameOrThrow(gameId);
        _intentionManager.ThrowIfForbidden(GameIntention.InviteReader, game);

        var user = await _userLookupService.GetAsync(username);
        await ValidateInvitation(game, user.UserId, ct);

        var entity = new CreateGameInvitationEntity
        {
            TokenId = _guidFactory.Create(),
            GameId = gameId,
            UserId = user.UserId,
            CreatorId = _identityProvider.Current.User.UserId,
            TokenType = TokenType.GameReaderInvitation,
            CreatedUtc = _dateTimeProvider.Now
        };

        var token = await _repository.InvalidateAndCreateInvitation(entity, ct);
        await _producer.SendAsync(EventType.ReaderInvitationCreated, token.TokenId);

        return await GetInvitationInfo(token.TokenId, ct);
    }

    public async Task<GameInvitation> InviteAssistant(Guid gameId, string username, CancellationToken ct = default)
    {
        var game = await GetGameOrThrow(gameId);
        _intentionManager.ThrowIfForbidden(GameIntention.InviteAssistant, game);

        var user = await _userLookupService.GetAsync(username);
        await ValidateInvitation(game, user.UserId, ct);

        var entity = new CreateGameInvitationEntity
        {
            TokenId = _guidFactory.Create(),
            GameId = gameId,
            UserId = user.UserId,
            CreatorId = _identityProvider.Current.User.UserId,
            TokenType = TokenType.GameAssistantInvitation,
            CreatedUtc = _dateTimeProvider.Now
        };

        var token = await _repository.InvalidateAndCreateInvitation(entity, ct);
        await _producer.SendAsync(EventType.AssignmentRequestCreated, token.TokenId);

        return await GetInvitationInfo(token.TokenId, ct);
    }

    public async Task AcceptInvitation(Guid tokenId, CancellationToken ct = default)
    {
        var (token, info) = await _repository.GetInvitation(tokenId, ct);
        if (token == null || info == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.InvitationNotFound);
        }

        // Check if invitation has expired
        var expiresUtc = token.CreatedUtc.AddDays(InvitationExpirationDays);
        if (_dateTimeProvider.Now > expiresUtc)
        {
            throw new HttpException(HttpStatusCode.Gone, RefusalMessage.InvitationExpired);
        }

        var currentUserId = _identityProvider.Current.User.UserId;
        if (token.UserId != currentUserId)
        {
            throw new HttpException(HttpStatusCode.Forbidden, RefusalMessage.InvitationNotForYou);
        }

        var gameId = token.EntityId!.Value;

        switch (token.TokenType)
        {
            case TokenType.GamePlayerInvitation:
                // Player invitation acceptance - just mark token as used
                // Character creation is a separate step
                await _repository.RemoveInvitation(tokenId, ct);
                await _producer.SendAsync(EventType.PlayerInvitationAccepted, tokenId);
                break;

            case TokenType.GameReaderInvitation:
                // Auto-subscribe as reader
                await _subscriptionService.SubscribeAsync(gameId, ct);
                await _repository.RemoveInvitation(tokenId, ct);
                await _producer.SendAsync(EventType.ReaderInvitationAccepted, tokenId);
                break;

            case TokenType.GameAssistantInvitation:
                // Add as assistant
                var addEntity = new AddAssistantEntity
                {
                    GameAssistantId = _guidFactory.Create(),
                    GameId = gameId,
                    UserId = currentUserId,
                    JoinedUtc = _dateTimeProvider.Now
                };
                await _repository.AddAssistant(addEntity, ct);
                await _repository.RemoveInvitation(tokenId, ct);
                await _producer.SendAsync(EventType.AssignmentRequestAccepted, tokenId);
                break;

            default:
                throw new HttpException(HttpStatusCode.BadRequest, "Неизвестный тип приглашения");
        }
    }

    public async Task RejectInvitation(Guid tokenId, CancellationToken ct = default)
    {
        var (token, _) = await _repository.GetInvitation(tokenId, ct);
        if (token == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.InvitationNotFound);
        }

        var currentUserId = _identityProvider.Current.User.UserId;
        if (token.UserId != currentUserId)
        {
            throw new HttpException(HttpStatusCode.Forbidden, RefusalMessage.InvitationNotForYou);
        }

        await _repository.RemoveInvitation(tokenId, ct);

        var eventType = token.TokenType switch
        {
            TokenType.GamePlayerInvitation => EventType.PlayerInvitationRejected,
            TokenType.GameReaderInvitation => EventType.ReaderInvitationRejected,
            TokenType.GameAssistantInvitation => EventType.AssignmentRequestRejected,
            _ => EventType.Unknown
        };

        if (eventType != EventType.Unknown)
        {
            await _producer.SendAsync(eventType, tokenId);
        }
    }

    public async Task CancelInvitation(Guid tokenId, CancellationToken ct = default)
    {
        var (token, _) = await _repository.GetInvitation(tokenId, ct);
        if (token == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.InvitationNotFound);
        }

        var game = await GetGameOrThrow(token.EntityId!.Value);
        _intentionManager.ThrowIfForbidden(GameIntention.CancelInvitation, game);

        await _repository.RemoveInvitation(tokenId, ct);
    }

    #endregion

    #region Users

    public async Task<IEnumerable<GameUser>> GetUsers(Guid gameId, CancellationToken ct = default)
    {
        // The list is public for a game the reader may open, and that half of the
        // sentence was the half nobody established: the repository filters only
        // on IsRemoved, so an identifier was enough to enumerate the players of a
        // game hidden from its holder. Fetching through GetGameOrThrow applies
        // the reader's scope, the intention confirms the right to read.
        var game = await GetGameOrThrow(gameId);
        _intentionManager.ThrowIfForbidden(GameIntention.Read, game);
        return await _repository.GetUsers(gameId, ct);
    }

    public async Task<IEnumerable<GameInvitation>> GetPendingInvitations(Guid gameId, CancellationToken ct = default)
    {
        var game = await GetGameOrThrow(gameId);
        _intentionManager.ThrowIfForbidden(GameIntention.Edit, game);
        return await _repository.GetPendingInvitations(gameId, ct);
    }

    public async Task<IEnumerable<GameInvitation>> GetUserInvitations(CancellationToken ct = default)
    {
        var userId = _identityProvider.Current.User.UserId;
        return await _repository.GetUserInvitations(userId, ct);
    }

    public async Task RemoveUser(Guid gameId, Guid userId, CancellationToken ct = default)
    {
        var game = await GetGameOrThrow(gameId);
        _intentionManager.ThrowIfForbidden(GameIntention.RemoveUser, game);

        // Cannot remove master
        if (game.Master.UserId == userId)
        {
            throw new HttpException(HttpStatusCode.Forbidden, "Нельзя удалить мастера игры");
        }

        // Only assistants can be removed from a game
        // Players are removed by deleting their characters (see CharacterService)
        // Readers cannot be removed - they can only unsubscribe themselves
        await _repository.RemoveAssistant(gameId, userId, ct);
    }

    #endregion

    #region Helpers

    private async Task<GameDto> GetGameOrThrow(Guid gameId)
    {
        var currentUserId = _identityProvider.Current?.User?.UserId ?? Guid.Empty;
        var game = await _gameRepository.GetGame(gameId, currentUserId);
        if (game == null)
        {
            throw new HttpException(HttpStatusCode.Gone, RefusalMessage.GameNotFound);
        }
        return game;
    }

    private async Task ValidateInvitation(GameDto game, Guid userId, CancellationToken ct = default)
    {
        var currentUserId = _identityProvider.Current.User.UserId;

        // Check content blacklist
        if (game.IsBlacklisted(userId))
        {
            throw new HttpException(HttpStatusCode.Forbidden, RefusalMessage.CannotInviteBlacklistedUser);
        }

        // Check personal blacklist - cannot invite someone you've blocked
        if (await _userBlacklistChecker.IsBlockedAsync(currentUserId, userId, ct))
        {
            throw new HttpException(HttpStatusCode.UnprocessableEntity, RefusalMessage.CannotInviteBlockedUser);
        }
    }

    private async Task<GameInvitation> GetInvitationInfo(Guid tokenId, CancellationToken ct)
    {
        var (_, info) = await _repository.GetInvitation(tokenId, ct);
        return info ?? throw new HttpException(HttpStatusCode.InternalServerError, "Не удалось получить приглашение");
    }

    #endregion
}
