using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Common.Authorization;
using DM.Services.Common.BusinessProcesses.Tokens;
using DM.Services.Core.Dto.Enums;
using DM.Services.Core.Exceptions;
using DM.Services.DataAccess.BusinessObjects.Users;
using DM.Services.DataAccess.RelationalStorage;
using DM.Services.Game.Authorization;
using DM.Services.Game.BusinessProcesses.Games.Reading;
using DM.Services.Game.BusinessProcesses.Readers.Subscribing;
using DM.Services.MessageQueuing.GeneralBus;

namespace DM.Services.Game.BusinessProcesses.Games.Invitations;

/// <inheritdoc />
internal class InvitationService : IInvitationService
{
    private readonly IIdentityProvider _identityProvider;
    private readonly ITokenFactory _tokenFactory;
    private readonly IUpdateBuilderFactory _updateBuilderFactory;
    private readonly IInvitationRepository _repository;
    private readonly IInvokedEventProducer _producer;
    private readonly IIntentionManager _intentionManager;
    private readonly IGameReadingService _gameReadingService;
    private readonly IReadingSubscribingService _readerService;

    /// <inheritdoc />
    public InvitationService(
        IIdentityProvider identityProvider,
        ITokenFactory tokenFactory,
        IUpdateBuilderFactory updateBuilderFactory,
        IInvitationRepository repository,
        IInvokedEventProducer producer,
        IIntentionManager intentionManager,
        IGameReadingService gameReadingService,
        IReadingSubscribingService readerService)
    {
        _identityProvider = identityProvider;
        _tokenFactory = tokenFactory;
        _updateBuilderFactory = updateBuilderFactory;
        _repository = repository;
        _producer = producer;
        _intentionManager = intentionManager;
        _gameReadingService = gameReadingService;
        _readerService = readerService;
    }

    /// <inheritdoc />
    public async Task<Token> CreatePlayerInvitation(Guid gameId, Guid userId)
    {
        var game = await _gameReadingService.GetGame(gameId);
        _intentionManager.ThrowIfForbidden(GameIntention.InvitePlayer, game);

        return await CreateInvitation(gameId, userId, TokenType.PlayerInvitation, EventType.PlayerInvitationCreated);
    }

    /// <inheritdoc />
    public async Task<Token> CreateReaderInvitation(Guid gameId, Guid userId)
    {
        var game = await _gameReadingService.GetGame(gameId);
        _intentionManager.ThrowIfForbidden(GameIntention.InviteReader, game);

        return await CreateInvitation(gameId, userId, TokenType.ReaderInvitation, EventType.ReaderInvitationCreated);
    }

    private async Task<Token> CreateInvitation(Guid gameId, Guid userId, TokenType type, EventType eventType)
    {
        // Invalidate existing invitations of same type for this user
        var existingInvites = await _repository.FindInvitations(gameId, userId, type);
        var updates = existingInvites.Select(id =>
            _updateBuilderFactory.Create<Token>(id).Field(t => t.IsRemoved, true));

        var token = _tokenFactory.Create(userId, gameId, type);
        await _repository.InvalidateAndCreate(updates, token);
        await _producer.Send(eventType, token.TokenId);

        return token;
    }

    /// <inheritdoc />
    public Task AcceptPlayerInvitation(Guid tokenId) =>
        ProcessInvitation(tokenId, TokenType.PlayerInvitation, true);

    /// <inheritdoc />
    public Task RejectPlayerInvitation(Guid tokenId) =>
        ProcessInvitation(tokenId, TokenType.PlayerInvitation, false);

    /// <inheritdoc />
    public async Task AcceptReaderInvitation(Guid tokenId)
    {
        var userId = _identityProvider.Current.User.UserId;
        var gameId = await _repository.FindGameByToken(tokenId, userId, TokenType.ReaderInvitation);
        if (!gameId.HasValue)
        {
            throw new HttpException(HttpStatusCode.Gone,
                "Invitation is invalid or expired");
        }

        // Auto-subscribe as reader
        await _readerService.Subscribe(gameId.Value);

        // Mark token as used
        var updateToken = _updateBuilderFactory.Create<Token>(tokenId).Field(t => t.IsRemoved, true);
        await _repository.Update(updateToken);
        await _producer.Send(EventType.ReaderInvitationAccepted, tokenId);
    }

    /// <inheritdoc />
    public Task RejectReaderInvitation(Guid tokenId) =>
        ProcessInvitation(tokenId, TokenType.ReaderInvitation, false);

    private async Task ProcessInvitation(Guid tokenId, TokenType type, bool accept)
    {
        var userId = _identityProvider.Current.User.UserId;
        var gameId = await _repository.FindGameByToken(tokenId, userId, type);
        if (!gameId.HasValue)
        {
            throw new HttpException(HttpStatusCode.Gone,
                "Invitation is invalid or expired");
        }

        var updateToken = _updateBuilderFactory.Create<Token>(tokenId).Field(t => t.IsRemoved, true);
        await _repository.Update(updateToken);

        var eventType = (type, accept) switch
        {
            (TokenType.PlayerInvitation, true) => EventType.PlayerInvitationAccepted,
            (TokenType.PlayerInvitation, false) => EventType.PlayerInvitationRejected,
            (TokenType.ReaderInvitation, true) => EventType.ReaderInvitationAccepted,
            (TokenType.ReaderInvitation, false) => EventType.ReaderInvitationRejected,
            _ => EventType.Unknown
        };

        if (eventType != EventType.Unknown)
        {
            await _producer.Send(eventType, tokenId);
        }
    }

    /// <inheritdoc />
    public async Task CancelInvitation(Guid tokenId)
    {
        var invitation = await _repository.GetInvitation(tokenId);
        if (invitation == null)
        {
            throw new HttpException(HttpStatusCode.Gone, "Invitation not found");
        }

        var game = await _gameReadingService.GetGame(invitation.GameId);
        _intentionManager.ThrowIfForbidden(GameIntention.CancelInvitation, game);

        var updateToken = _updateBuilderFactory.Create<Token>(tokenId).Field(t => t.IsRemoved, true);
        await _repository.Update(updateToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<InvitationInfo>> GetPendingInvitations(Guid gameId)
    {
        var game = await _gameReadingService.GetGame(gameId);
        _intentionManager.ThrowIfForbidden(GameIntention.Edit, game);

        return await _repository.GetPendingInvitations(gameId);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<InvitationInfo>> GetUserPendingInvitations()
    {
        var userId = _identityProvider.Current.User.UserId;
        return await _repository.GetUserPendingInvitations(userId);
    }
}
