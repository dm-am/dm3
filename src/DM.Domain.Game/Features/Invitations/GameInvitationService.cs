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
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Events;
using GameDto = DM.Domain.Game.Features.Games.Game;

namespace DM.Domain.Game.Features.Invitations;

/// <inheritdoc />
internal class GameInvitationService : IGameInvitationService
{
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
    public Task<GameInvitation> InvitePlayer(Guid gameId, string username, CancellationToken ct = default) =>
        Invite(gameId, username, GameIntention.InvitePlayer,
            TokenType.GamePlayerInvitation, EventType.PlayerInvitationCreated, ct);

    public Task<GameInvitation> InviteReader(Guid gameId, string username, CancellationToken ct = default) =>
        Invite(gameId, username, GameIntention.InviteReader,
            TokenType.GameReaderInvitation, EventType.ReaderInvitationCreated, ct);

    public Task<GameInvitation> InviteAssistant(Guid gameId, string username, CancellationToken ct = default) =>
        Invite(gameId, username, GameIntention.InviteAssistant,
            TokenType.GameAssistantInvitation, EventType.AssignmentRequestCreated, ct);

    public async Task AcceptInvitation(Guid tokenId, CancellationToken ct = default)
    {
        var (token, info) = await _repository.GetInvitation(tokenId, ct);
        if (token == null || info == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.InvitationNotFound);
        }

        // Check if invitation has expired
        var expiresUtc = InvitationPolicy.ExpiresAt(token.CreatedUtc);
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
                //
                // The announcement belongs to the acceptance that happened. The
                // link is spent by a conditional statement, so of two acceptances
                // meeting on one link only one spends anything, and announcing
                // from the other tells the game a seat was taken twice.
                if (await _repository.RemoveInvitation(tokenId, ct) == 1)
                {
                    await _producer.SendAsync(EventType.PlayerInvitationAccepted, tokenId);
                }

                break;

            case TokenType.GameReaderInvitation:
                // The subscription first and the token second, on purpose: subscribing
                // is idempotent, so a break between the two is repaired by following
                // the same link again. The other order would spend the invitation and
                // leave nothing for the second attempt to do.
                await _subscriptionService.SubscribeAsync(gameId, ct);
                if (await _repository.RemoveInvitation(tokenId, ct) == 1)
                {
                    await _producer.SendAsync(EventType.ReaderInvitationAccepted, tokenId);
                }

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
                // The announcement belongs to the acceptance that happened. Two
                // accepts of one link both leave the person an assistant, and the
                // second one spent nothing: repeating the event there tells the
                // game twice that a seat was taken once.
                if (await _repository.AcceptAssistantInvitation(addEntity, tokenId, ct))
                {
                    await _producer.SendAsync(EventType.AssignmentRequestAccepted, tokenId);
                }

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

        // The announcement belongs to the rejection that happened, the same as on
        // the accepting side: the link is spent by a conditional statement, so of
        // two clicks meeting on it only one spends anything, and announcing from
        // the other tells the master the invitation was turned down twice.
        var rejected = await _repository.RemoveInvitation(tokenId, ct) == 1;

        var eventType = token.TokenType switch
        {
            TokenType.GamePlayerInvitation => EventType.PlayerInvitationRejected,
            TokenType.GameReaderInvitation => EventType.ReaderInvitationRejected,
            TokenType.GameAssistantInvitation => EventType.AssignmentRequestRejected,
            _ => EventType.Unknown
        };

        if (rejected && eventType != EventType.Unknown)
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
        // The list is a panel of the settings page, so it follows the page:
        // EditSettings, same as the blog side. Reading who was invited is not
        // managing the roster — creating and revoking invitations stay on the
        // lead-only InvitePlayer / InviteReader / CancelInvitation.
        _intentionManager.ThrowIfForbidden(GameIntention.EditSettings, game);
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
        if (game.Master?.UserId == userId)
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

    /// <summary>
    /// One invitation, whichever of the three roles it is for.
    /// </summary>
    /// <remarks>
    /// The three public methods differ in three literals and in nothing else: the
    /// gate they check, the token they write and the event they announce. The
    /// blog module already answers the same question this way
    /// (BlogInvitationService.CreateInvitation).
    /// </remarks>
    private async Task<GameInvitation> Invite(
        Guid gameId,
        string username,
        GameIntention intention,
        TokenType tokenType,
        EventType eventType,
        CancellationToken ct)
    {
        var game = await GetGameOrThrow(gameId);
        _intentionManager.ThrowIfForbidden(intention, game);

        var user = await _userLookupService.GetAsync(username);
        await ValidateInvitation(user.UserId, ct);

        var entity = new CreateGameInvitationEntity
        {
            TokenId = _guidFactory.Create(),
            GameId = gameId,
            UserId = user.UserId,
            CreatorId = _identityProvider.Current.User.UserId,
            TokenType = tokenType,
            CreatedUtc = _dateTimeProvider.Now
        };

        var token = await _repository.InvalidateAndCreateInvitation(entity, ct);
        await _producer.SendAsync(eventType, token.TokenId);

        return await GetInvitationInfo(token.TokenId, ct);
    }

    private async Task<GameDto> GetGameOrThrow(Guid gameId)
    {
        var currentUserId = _identityProvider.Current?.User?.UserId ?? Guid.Empty;
        // The invitation paths are lead-only on their own gates, so the
        // premoderation rank buys nothing here: a mentor who may judge a game
        // still may not staff it.
        var game = await _gameRepository.GetGame(gameId, currentUserId, mayJudgePremoderation: false);
        if (game == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.GameNotFound);
        }
        return game;
    }

    private async Task ValidateInvitation(Guid userId, CancellationToken ct = default)
    {
        var currentUserId = _identityProvider.Current.User.UserId;

        // The game's own blacklist is not asked: inviting somebody the game
        // listed is the lead's own decision. Entering the list withdraws the
        // invitations already sent (GameBlacklistService), not the right to send
        // a new one.
        //
        // The invitee's personal blacklist: an invitation reaches its addressee
        // signed with the inviter's name, so it is not created for somebody who
        // blocked the inviter. The inviter's own list is not asked - it restricts
        // the people on it, not its owner (RefusalMessage.CannotInviteThisUser).
        if (await _userBlacklistChecker.IsBlockedAsync(userId, currentUserId, ct))
        {
            throw new HttpException(HttpStatusCode.Forbidden, RefusalMessage.CannotInviteThisUser);
        }
    }

    private async Task<GameInvitation> GetInvitationInfo(Guid tokenId, CancellationToken ct)
    {
        var (_, info) = await _repository.GetInvitation(tokenId, ct);
        return info ?? throw new HttpException(HttpStatusCode.InternalServerError, "Не удалось получить приглашение");
    }

    #endregion
}
