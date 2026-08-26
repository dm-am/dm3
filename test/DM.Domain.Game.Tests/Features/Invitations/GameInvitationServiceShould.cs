using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Blacklists;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Events;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Users;
using DM.Domain.Game.Authorization;
using DM.Domain.Game.Features.Games;
using GameDto = DM.Domain.Game.Features.Games.Game;
using DM.Domain.Game.Features.Invitations;
using DM.Domain.Game.Features.Subscriptions;
using DM.Testing.Dsl;
using DM.Testing;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Domain.Game.Tests.Features.Invitations;

public class GameInvitationServiceShould : UnitTestBase
{
    private readonly IIntentionManager _intentionManager;
    private readonly IGameRepository _gameRepository;
    private readonly IUserLookupService _userLookupService;
    private readonly IGameInvitationRepository _repository;
    private readonly IGameSubscriptionService _subscriptionService;
    private readonly IUserBlacklistChecker _userBlacklistChecker;
    private readonly IEventProducer _producer;
    private readonly IIdentityProvider _identityProvider;
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly GameInvitationService _service;
    private readonly Guid _currentUserId;

    public GameInvitationServiceShould()
    {
        _currentUserId = Guid.NewGuid();
        _identityProvider = Mock<IIdentityProvider>();
        _identityProvider.Current.Returns(Identities.User(_currentUserId, UserRole.RegularUser));

        _intentionManager = Mock<IIntentionManager>();

        _gameRepository = Mock<IGameRepository>();
        _userLookupService = Mock<IUserLookupService>();
        _repository = Mock<IGameInvitationRepository>();
        _subscriptionService = Mock<IGameSubscriptionService>();
        _userBlacklistChecker = Mock<IUserBlacklistChecker>();
        _userBlacklistChecker.IsBlockedAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(false);

        _producer = Mock<IEventProducer>();
        _producer.SendAsync(Arg.Any<EventType>(), Arg.Any<Guid>()).Returns(Task.CompletedTask);

        _dateTimeProvider = Mock<IDateTimeProvider>();
        _dateTimeProvider.Now.Returns(DateTimeOffset.UtcNow);

        _guidFactory = Mock<IGuidFactory>();
        _guidFactory.Create().Returns(Guid.NewGuid());

        _service = new GameInvitationService(
            _identityProvider,
            _intentionManager,
            _gameRepository,
            _userLookupService,
            _repository,
            _subscriptionService,
            _userBlacklistChecker,
            _producer,
            _dateTimeProvider,
            _guidFactory);
    }

    [Fact]
    public async Task AuthorizeInvitePlayerAction()
    {
        var gameId = Guid.NewGuid();
        var username = "testuser";
        var userId = Guid.NewGuid();
        var game = new GameDto
        {
            Id = gameId,
            Master = new GeneralUser { UserId = Guid.NewGuid(), Username = "Author" },
            BlacklistedUsers = Array.Empty<BlacklistedUser>()
        };
        _gameRepository.GetGame(gameId, _currentUserId, Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(game);
        _userLookupService.GetAsync(username).Returns(new GeneralUser { UserId = userId });
        _repository.InvalidateAndCreateInvitation(Arg.Any<CreateGameInvitationEntity>(), Arg.Any<CancellationToken>())
            .Returns(new GameInvitationToken { TokenId = Guid.NewGuid() });
        _repository.GetInvitation(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((new GameInvitationToken(), new GameInvitation()));

        await _service.InvitePlayer(gameId, username);

        _intentionManager.Received(1).ThrowIfForbidden(GameIntention.InvitePlayer, game);
    }

    /// <summary>
    /// The pending list is a panel of the settings page, so it follows the page
    /// rather than the roster: EditSettings admits the curating mentor, while
    /// InvitePlayer and CancelInvitation above keep the leads' own gates. Read
    /// and write parting company here is the point — the mentor sees who was
    /// invited and cannot invite or revoke.
    /// </summary>
    [Fact]
    public async Task AuthorizePendingInvitationsReadWithTheSettingsIntention()
    {
        var gameId = Guid.NewGuid();
        var game = new GameDto
        {
            Id = gameId,
            Master = new GeneralUser { UserId = Guid.NewGuid(), Username = "Author" },
            BlacklistedUsers = Array.Empty<BlacklistedUser>()
        };
        _gameRepository.GetGame(gameId, _currentUserId, Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(game);
        _repository.GetPendingInvitations(gameId, Arg.Any<CancellationToken>()).Returns(Array.Empty<GameInvitation>());

        await _service.GetPendingInvitations(gameId);

        _intentionManager.Received(1).ThrowIfForbidden(GameIntention.EditSettings, game);
        _intentionManager.DidNotReceive().ThrowIfForbidden(GameIntention.Edit, game);
    }

    [Fact]
    public async Task PublishEventWhenInvitingPlayer()
    {
        var gameId = Guid.NewGuid();
        var username = "testuser";
        var userId = Guid.NewGuid();
        var tokenId = Guid.NewGuid();
        var game = new GameDto
        {
            Id = gameId,
            Master = new GeneralUser { UserId = Guid.NewGuid(), Username = "Author" },
            BlacklistedUsers = Array.Empty<BlacklistedUser>()
        };
        _gameRepository.GetGame(gameId, _currentUserId, Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(game);
        _userLookupService.GetAsync(username).Returns(new GeneralUser { UserId = userId });
        _repository.InvalidateAndCreateInvitation(Arg.Any<CreateGameInvitationEntity>(), Arg.Any<CancellationToken>())
            .Returns(new GameInvitationToken { TokenId = tokenId });
        _repository.GetInvitation(tokenId, Arg.Any<CancellationToken>())
            .Returns((new GameInvitationToken(), new GameInvitation()));

        await _service.InvitePlayer(gameId, username);

        await _producer.Received(1).SendAsync(EventType.PlayerInvitationCreated, tokenId);
    }

    [Fact]
    public async Task ThrowForbiddenWhenInvitingBlacklistedUser()
    {
        var gameId = Guid.NewGuid();
        var username = "testuser";
        var userId = Guid.NewGuid();
        var game = new GameDto
        {
            Id = gameId,
            Master = new GeneralUser { UserId = Guid.NewGuid(), Username = "Author" },
            BlacklistedUsers = new[] { new BlacklistedUser { UserId = userId } }
        };
        _gameRepository.GetGame(gameId, _currentUserId, Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(game);
        _userLookupService.GetAsync(username).Returns(new GeneralUser { UserId = userId });

        var act = async () => await _service.InvitePlayer(gameId, username);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AcceptPlayerInvitationAndRemoveToken()
    {
        var tokenId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var token = new GameInvitationToken
        {
            TokenId = tokenId,
            UserId = _currentUserId,
            EntityId = gameId,
            TokenType = TokenType.GamePlayerInvitation,
            CreatedUtc = DateTimeOffset.UtcNow
        };
        _repository.GetInvitation(tokenId, Arg.Any<CancellationToken>()).Returns((token, new GameInvitation()));
        _repository.RemoveInvitation(tokenId, Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        await _service.AcceptInvitation(tokenId);

        await _repository.Received(1).RemoveInvitation(tokenId, Arg.Any<CancellationToken>());
        await _producer.Received(1).SendAsync(EventType.PlayerInvitationAccepted, tokenId);
    }

    [Fact]
    public async Task AcceptReaderInvitationAndSubscribe()
    {
        var tokenId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var token = new GameInvitationToken
        {
            TokenId = tokenId,
            UserId = _currentUserId,
            EntityId = gameId,
            TokenType = TokenType.GameReaderInvitation,
            CreatedUtc = DateTimeOffset.UtcNow
        };
        _repository.GetInvitation(tokenId, Arg.Any<CancellationToken>()).Returns((token, new GameInvitation()));
        _repository.RemoveInvitation(tokenId, Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _subscriptionService.SubscribeAsync(gameId, Arg.Any<CancellationToken>())
            .Returns(new DM.Domain.Core.Subscriptions.Subscription());

        await _service.AcceptInvitation(tokenId);

        await _subscriptionService.Received(1).SubscribeAsync(gameId, Arg.Any<CancellationToken>());
        await _producer.Received(1).SendAsync(EventType.ReaderInvitationAccepted, tokenId);
    }

    [Fact]
    public async Task ThrowNotFoundWhenInvitationDoesNotExist()
    {
        var tokenId = Guid.NewGuid();
        _repository.GetInvitation(tokenId, Arg.Any<CancellationToken>())
            .Returns(((GameInvitationToken?)null, (GameInvitation?)null));

        var act = async () => await _service.AcceptInvitation(tokenId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound);
    }
}
