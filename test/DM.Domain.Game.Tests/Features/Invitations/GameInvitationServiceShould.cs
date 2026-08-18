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
using FluentAssertions;
using Moq;
using Xunit;

namespace DM.Domain.Game.Tests.Features.Invitations;

public class GameInvitationServiceShould : UnitTestBase
{
    private readonly Mock<IIntentionManager> _intentionManager;
    private readonly Mock<IGameRepository> _gameRepository;
    private readonly Mock<IUserLookupService> _userLookupService;
    private readonly Mock<IGameInvitationRepository> _repository;
    private readonly Mock<IGameSubscriptionService> _subscriptionService;
    private readonly Mock<IUserBlacklistChecker> _userBlacklistChecker;
    private readonly Mock<IEventProducer> _producer;
    private readonly Mock<IIdentityProvider> _identityProvider;
    private readonly Mock<IGuidFactory> _guidFactory;
    private readonly Mock<IDateTimeProvider> _dateTimeProvider;
    private readonly GameInvitationService _service;
    private readonly Guid _currentUserId;

    public GameInvitationServiceShould()
    {
        _currentUserId = Guid.NewGuid();
        _identityProvider = Mock<IIdentityProvider>();
        _identityProvider.Setup(p => p.Current).Returns(Identities.User(_currentUserId, UserRole.RegularUser));

        _intentionManager = Mock<IIntentionManager>();
        _intentionManager.Setup(m => m.ThrowIfForbidden(It.IsAny<GameIntention>(), It.IsAny<GameDto>()));

        _gameRepository = Mock<IGameRepository>();
        _userLookupService = Mock<IUserLookupService>();
        _repository = Mock<IGameInvitationRepository>();
        _subscriptionService = Mock<IGameSubscriptionService>();
        _userBlacklistChecker = Mock<IUserBlacklistChecker>();
        _userBlacklistChecker.Setup(c => c.IsBlockedAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _producer = Mock<IEventProducer>();
        _producer.Setup(p => p.SendAsync(It.IsAny<EventType>(), It.IsAny<Guid>())).Returns(Task.CompletedTask);

        _dateTimeProvider = Mock<IDateTimeProvider>();
        _dateTimeProvider.Setup(d => d.Now).Returns(DateTimeOffset.UtcNow);

        _guidFactory = Mock<IGuidFactory>();
        _guidFactory.Setup(g => g.Create()).Returns(Guid.NewGuid());

        _service = new GameInvitationService(
            _identityProvider.Object,
            _intentionManager.Object,
            _gameRepository.Object,
            _userLookupService.Object,
            _repository.Object,
            _subscriptionService.Object,
            _userBlacklistChecker.Object,
            _producer.Object,
            _dateTimeProvider.Object,
            _guidFactory.Object);
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
        _gameRepository.Setup(r => r.GetGame(gameId, _currentUserId, It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(game);
        _userLookupService.Setup(u => u.GetAsync(username)).ReturnsAsync(new GeneralUser { UserId = userId });
        _repository.Setup(r => r.InvalidateAndCreateInvitation(It.IsAny<CreateGameInvitationEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GameInvitationToken { TokenId = Guid.NewGuid() });
        _repository.Setup(r => r.GetInvitation(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new GameInvitationToken(), new GameInvitation()));

        await _service.InvitePlayer(gameId, username);

        _intentionManager.Verify(m => m.ThrowIfForbidden(GameIntention.InvitePlayer, game), Times.Once);
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
        _gameRepository.Setup(r => r.GetGame(gameId, _currentUserId, It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(game);
        _repository.Setup(r => r.GetPendingInvitations(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<GameInvitation>());

        await _service.GetPendingInvitations(gameId);

        _intentionManager.Verify(m => m.ThrowIfForbidden(GameIntention.EditSettings, game), Times.Once);
        _intentionManager.Verify(m => m.ThrowIfForbidden(GameIntention.Edit, game), Times.Never);
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
        _gameRepository.Setup(r => r.GetGame(gameId, _currentUserId, It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(game);
        _userLookupService.Setup(u => u.GetAsync(username)).ReturnsAsync(new GeneralUser { UserId = userId });
        _repository.Setup(r => r.InvalidateAndCreateInvitation(It.IsAny<CreateGameInvitationEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GameInvitationToken { TokenId = tokenId });
        _repository.Setup(r => r.GetInvitation(tokenId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new GameInvitationToken(), new GameInvitation()));

        await _service.InvitePlayer(gameId, username);

        _producer.Verify(p => p.SendAsync(EventType.PlayerInvitationCreated, tokenId), Times.Once);
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
        _gameRepository.Setup(r => r.GetGame(gameId, _currentUserId, It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(game);
        _userLookupService.Setup(u => u.GetAsync(username)).ReturnsAsync(new GeneralUser { UserId = userId });

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
        _repository.Setup(r => r.GetInvitation(tokenId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((token, new GameInvitation()));
        _repository.Setup(r => r.RemoveInvitation(tokenId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _service.AcceptInvitation(tokenId);

        _repository.Verify(r => r.RemoveInvitation(tokenId, It.IsAny<CancellationToken>()), Times.Once);
        _producer.Verify(p => p.SendAsync(EventType.PlayerInvitationAccepted, tokenId), Times.Once);
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
        _repository.Setup(r => r.GetInvitation(tokenId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((token, new GameInvitation()));
        _repository.Setup(r => r.RemoveInvitation(tokenId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _subscriptionService.Setup(s => s.SubscribeAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DM.Domain.Core.Subscriptions.Subscription());

        await _service.AcceptInvitation(tokenId);

        _subscriptionService.Verify(s => s.SubscribeAsync(gameId, It.IsAny<CancellationToken>()), Times.Once);
        _producer.Verify(p => p.SendAsync(EventType.ReaderInvitationAccepted, tokenId), Times.Once);
    }

    [Fact]
    public async Task ThrowNotFoundWhenInvitationDoesNotExist()
    {
        var tokenId = Guid.NewGuid();
        _repository.Setup(r => r.GetInvitation(tokenId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(((GameInvitationToken?)null, (GameInvitation?)null));

        var act = async () => await _service.AcceptInvitation(tokenId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound);
    }
}
