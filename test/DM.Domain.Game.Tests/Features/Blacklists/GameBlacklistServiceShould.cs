using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Events;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Users;
using DM.Domain.Game.Authorization;
using DM.Domain.Game.Features.Blacklists;
using DM.Domain.Game.Features.Characters;
using DM.Domain.Game.Features.Games;
using GameDto = DM.Domain.Game.Features.Games.Game;
using DM.Domain.Game.Features.Invitations;
using DM.Testing.Dsl;
using DM.Testing;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using Xunit;

namespace DM.Domain.Game.Tests.Features.Blacklists;

public class GameBlacklistServiceShould : UnitTestBase
{
    private readonly Mock<IGameService> _gameService;
    private readonly Mock<IIntentionManager> _intentionManager;
    private readonly Mock<IIdentityProvider> _identityProvider;
    private readonly Mock<IUserLookupService> _userLookupService;
    private readonly Mock<IGameBlacklistRepository> _repository;
    private readonly Mock<IGameInvitationRepository> _invitationRepository;
    private readonly Mock<ICharacterRepository> _characterRepository;
    private readonly Mock<IEventProducer> _producer;
    private readonly GameBlacklistService _service;
    private readonly Guid _currentUserId;

    public GameBlacklistServiceShould()
    {
        var validator = Mock<IValidator<OperateBlacklistLink>>();
        validator.Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<OperateBlacklistLink>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _gameService = Mock<IGameService>();
        _intentionManager = Mock<IIntentionManager>();
        _intentionManager.Setup(m => m.ThrowIfForbidden(It.IsAny<GameIntention>(), It.IsAny<GameDto>()));

        _currentUserId = Guid.NewGuid();
        _identityProvider = Mock<IIdentityProvider>();
        _identityProvider.Setup(p => p.Current).Returns(Identities.User(_currentUserId, UserRole.RegularUser));

        _userLookupService = Mock<IUserLookupService>();
        _repository = Mock<IGameBlacklistRepository>();
        _invitationRepository = Mock<IGameInvitationRepository>();
        _characterRepository = Mock<ICharacterRepository>();
        _producer = Mock<IEventProducer>();
        _producer.Setup(p => p.SendAsync(It.IsAny<EventType>(), It.IsAny<Guid>())).Returns(Task.CompletedTask);

        _service = new GameBlacklistService(
            validator.Object,
            _gameService.Object,
            _intentionManager.Object,
            _identityProvider.Object,
            _userLookupService.Object,
            _repository.Object,
            _invitationRepository.Object,
            _characterRepository.Object,
            _producer.Object);
    }

    private static GameDetails CreateGame(Guid gameId, BlacklistedUser[]? blacklistedUsers = null) => new GameDetails
    {
        Id = gameId,
        Master = new GeneralUser { UserId = Guid.NewGuid(), Username = "Author" },
        BlacklistedUsers = blacklistedUsers ?? []
    };

    [Fact]
    public async Task AuthorizeGetBlacklist()
    {
        var gameId = Guid.NewGuid();
        var game = CreateGame(gameId);
        _gameService.Setup(s => s.GetAsync(gameId)).ReturnsAsync(game);
        _repository.Setup(r => r.Get(gameId)).ReturnsAsync(Array.Empty<GeneralUser>());

        await _service.Get(gameId);

        _intentionManager.Verify(m => m.ThrowIfForbidden(GameIntention.Edit, It.Is<GameDto>(g => g.Id == gameId)), Times.Once);
    }

    [Fact]
    public async Task AuthorizeAddToBlacklist()
    {
        var gameId = Guid.NewGuid();
        var username = "testuser";
        var userId = Guid.NewGuid();
        var game = CreateGame(gameId);
        _gameService.Setup(s => s.GetAsync(gameId)).ReturnsAsync(game);
        _userLookupService.Setup(u => u.FindUserIdAsync(username, It.IsAny<CancellationToken>())).ReturnsAsync((true, userId));
        _repository.Setup(r => r.Add(gameId, userId, _currentUserId))
            .ReturnsAsync(new GeneralUser { UserId = userId });
        _invitationRepository.Setup(r => r.CancelInvitationsForUser(gameId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<CancelledInvitation>());

        await _service.Add(new OperateBlacklistLink { GameId = gameId, Username = username });

        _intentionManager.Verify(m => m.ThrowIfForbidden(GameIntention.Edit, It.Is<GameDto>(g => g.Id == gameId)), Times.Once);
    }

    [Fact]
    public async Task ThrowConflictWhenUserAlreadyBlacklisted()
    {
        var gameId = Guid.NewGuid();
        var username = "testuser";
        var userId = Guid.NewGuid();
        var game = CreateGame(gameId, new[] { new BlacklistedUser { UserId = userId } });
        _gameService.Setup(s => s.GetAsync(gameId)).ReturnsAsync(game);
        _userLookupService.Setup(u => u.FindUserIdAsync(username, It.IsAny<CancellationToken>())).ReturnsAsync((true, userId));

        var act = async () => await _service.Add(new OperateBlacklistLink { GameId = gameId, Username = username });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CancelInvitationsWhenAddingToBlacklist()
    {
        var gameId = Guid.NewGuid();
        var username = "testuser";
        var userId = Guid.NewGuid();
        var tokenId = Guid.NewGuid();
        var game = CreateGame(gameId);
        _gameService.Setup(s => s.GetAsync(gameId)).ReturnsAsync(game);
        _userLookupService.Setup(u => u.FindUserIdAsync(username, It.IsAny<CancellationToken>())).ReturnsAsync((true, userId));
        _repository.Setup(r => r.Add(gameId, userId, _currentUserId))
            .ReturnsAsync(new GeneralUser { UserId = userId });
        _invitationRepository.Setup(r => r.CancelInvitationsForUser(gameId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new CancelledInvitation { TokenId = tokenId, TokenType = TokenType.GamePlayerInvitation } });

        await _service.Add(new OperateBlacklistLink { GameId = gameId, Username = username });

        _producer.Verify(p => p.SendAsync(EventType.PlayerInvitationCancelled, tokenId), Times.Once);
        _characterRepository.Verify(r => r.DeclinePendingCharacters(gameId, userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PublishGameChangedEventAfterBlacklisting()
    {
        var gameId = Guid.NewGuid();
        var username = "testuser";
        var userId = Guid.NewGuid();
        var game = CreateGame(gameId);
        _gameService.Setup(s => s.GetAsync(gameId)).ReturnsAsync(game);
        _userLookupService.Setup(u => u.FindUserIdAsync(username, It.IsAny<CancellationToken>())).ReturnsAsync((true, userId));
        _repository.Setup(r => r.Add(gameId, userId, _currentUserId))
            .ReturnsAsync(new GeneralUser { UserId = userId });
        _invitationRepository.Setup(r => r.CancelInvitationsForUser(gameId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<CancelledInvitation>());

        await _service.Add(new OperateBlacklistLink { GameId = gameId, Username = username });

        _producer.Verify(p => p.SendAsync(EventType.ChangedGame, gameId), Times.Once);
    }
}
