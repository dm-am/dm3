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
using DM.Domain.Core.Subscriptions;
using DM.Domain.Core.Users;
using DM.Domain.Game.Authorization;
using DM.Domain.Game.Features.Blacklists;
using DM.Domain.Game.Features.Characters;
using DM.Domain.Game.Features.Games;
using GameDto = DM.Domain.Game.Features.Games.Game;
using DM.Domain.Game.Features.Invitations;
using DM.Testing.Dsl;
using DM.Testing;
using AwesomeAssertions;
using FluentValidation;
using FluentValidation.Results;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace DM.Domain.Game.Tests.Features.Blacklists;

public class GameBlacklistServiceShould : UnitTestBase
{
    private readonly IGameService _gameService;
    private readonly IIntentionManager _intentionManager;
    private readonly IIdentityProvider _identityProvider;
    private readonly IUserLookupService _userLookupService;
    private readonly IGameBlacklistRepository _repository;
    private readonly IGameInvitationRepository _invitationRepository;
    private readonly ICharacterRepository _characterRepository;
    private readonly IEventProducer _producer;
    private readonly GameBlacklistService _service;
    private readonly Guid _currentUserId;

    public GameBlacklistServiceShould()
    {
        var validator = Mock<IValidator<OperateBlacklistLink>>();
        validator.ValidateAsync(Arg.Any<ValidationContext<OperateBlacklistLink>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        _gameService = Mock<IGameService>();
        _intentionManager = Mock<IIntentionManager>();

        _currentUserId = Guid.NewGuid();
        _identityProvider = Mock<IIdentityProvider>();
        _identityProvider.Current.Returns(Identities.User(_currentUserId, UserRole.RegularUser));

        _userLookupService = Mock<IUserLookupService>();
        _repository = Mock<IGameBlacklistRepository>();
        _invitationRepository = Mock<IGameInvitationRepository>();
        _characterRepository = Mock<ICharacterRepository>();
        _producer = Mock<IEventProducer>();
        _producer.SendAsync(Arg.Any<EventType>(), Arg.Any<Guid>()).Returns(Task.CompletedTask);

        _service = new GameBlacklistService(
            validator,
            _gameService,
            _intentionManager,
            _identityProvider,
            _userLookupService,
            _repository,
            _invitationRepository,
            _characterRepository,
            _producer);
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
        _gameService.GetAsync(gameId).Returns(game);
        _repository.Get(gameId).Returns(Array.Empty<GeneralUser>());

        await _service.Get(gameId);

        _intentionManager.Received(1).ThrowIfForbidden(GameIntention.Edit, Arg.Is<GameDto>(g => g.Id == gameId));
    }

    [Fact]
    public async Task AuthorizeAddToBlacklist()
    {
        var gameId = Guid.NewGuid();
        var username = "testuser";
        var userId = Guid.NewGuid();
        var game = CreateGame(gameId);
        _gameService.GetAsync(gameId).Returns(game);
        _userLookupService.FindUserIdAsync(username, Arg.Any<CancellationToken>()).Returns((true, userId));
        _repository.Add(gameId, userId, _currentUserId).Returns(new GeneralUser { UserId = userId });
        _invitationRepository.CancelInvitationsForUser(gameId, userId, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<CancelledInvitation>());

        await _service.Add(new OperateBlacklistLink { GameId = gameId, Username = username });

        _intentionManager.Received(1).ThrowIfForbidden(GameIntention.Edit, Arg.Is<GameDto>(g => g.Id == gameId));
    }

    [Fact]
    public async Task ThrowConflictWhenUserAlreadyBlacklisted()
    {
        var gameId = Guid.NewGuid();
        var username = "testuser";
        var userId = Guid.NewGuid();
        var game = CreateGame(gameId, new[] { new BlacklistedUser { UserId = userId } });
        _gameService.GetAsync(gameId).Returns(game);
        _userLookupService.FindUserIdAsync(username, Arg.Any<CancellationToken>()).Returns((true, userId));

        var act = async () => await _service.Add(new OperateBlacklistLink { GameId = gameId, Username = username });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Conflict);
    }

    /// <summary>
    /// The request that lost the race is answered the way the check answers it.
    /// </summary>
    /// <remarks>
    /// The blacklist is read off the game a statement before the insert, so two
    /// requests for one user both pass that read. The unique index refuses the
    /// second insert and the repository hands it over as a duplicate; leaving that
    /// exception uncaught would have turned the same question into a 500, and the
    /// caller asked something whose answer is already true.
    /// </remarks>
    [Fact]
    public async Task AnswerConflictWhenTheInsertLosesTheRace()
    {
        var gameId = Guid.NewGuid();
        var username = "testuser";
        var userId = Guid.NewGuid();
        var game = CreateGame(gameId);
        _gameService.GetAsync(gameId).Returns(game);
        _userLookupService.FindUserIdAsync(username, Arg.Any<CancellationToken>()).Returns((true, userId));
        _repository.Add(gameId, userId, _currentUserId)
            .ThrowsAsync(new DuplicateEntityException("Duplicate game blacklist entry"));

        var act = async () => await _service.Add(new OperateBlacklistLink { GameId = gameId, Username = username });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Conflict &&
                        e.Message == RefusalMessage.AlreadyInBlacklist);
        await _producer.DidNotReceive().SendAsync(EventType.ChangedGame, gameId);
    }

    /// <summary>
    /// A subscriber is blacklisted as he stands, and the subscription is left
    /// alone.
    /// </summary>
    /// <remarks>
    /// The list closes writing, and a subscription is reading. The service used
    /// to refuse a subscriber with "remove him from the game first", and nothing
    /// removes a reader - so the player who had just left, whom the game
    /// subscribes by itself, was the one person who could never be put on the
    /// list. The service no longer holds the subscription table at all, which is
    /// what keeps the subscription: there is nothing here to end it with.
    /// </remarks>
    [Fact]
    public async Task BlacklistASubscriberAndKeepTheSubscription()
    {
        var gameId = Guid.NewGuid();
        var username = "reader";
        var userId = Guid.NewGuid();
        var game = CreateGame(gameId);
        _gameService.GetAsync(gameId).Returns(game);
        _userLookupService.FindUserIdAsync(username, Arg.Any<CancellationToken>()).Returns((true, userId));
        _repository.Add(gameId, userId, _currentUserId).Returns(new GeneralUser { UserId = userId });
        _invitationRepository.CancelInvitationsForUser(gameId, userId, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<CancelledInvitation>());

        await _service.Add(new OperateBlacklistLink { GameId = gameId, Username = username });

        await _repository.Received(1).Add(gameId, userId, _currentUserId);
        typeof(GameBlacklistService).GetConstructors().Single().GetParameters()
            .Should().NotContain(parameter => parameter.ParameterType == typeof(ISubscriptionRepository),
                "entering the list must not reach the subscription");
    }

    [Fact]
    public async Task RefuseToBlacklistAPlayerBeforeTheyLeave()
    {
        var gameId = Guid.NewGuid();
        var username = "player";
        var userId = Guid.NewGuid();
        var game = CreateGame(gameId);
        game.Players = [new GeneralUser { UserId = userId, Username = username }];
        _gameService.GetAsync(gameId).Returns(game);
        _userLookupService.FindUserIdAsync(username, Arg.Any<CancellationToken>()).Returns((true, userId));

        var act = async () => await _service.Add(new OperateBlacklistLink { GameId = gameId, Username = username });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Conflict && e.Message == RefusalMessage.RemoveFromGameFirst);
        await _repository.DidNotReceive().Add(gameId, userId, _currentUserId);
    }

    [Fact]
    public async Task RefuseToBlacklistTheMasterInTheSharedWords()
    {
        var gameId = Guid.NewGuid();
        var game = CreateGame(gameId);
        var masterId = game.Master!.UserId;
        _gameService.GetAsync(gameId).Returns(game);
        _userLookupService.FindUserIdAsync("Author", Arg.Any<CancellationToken>()).Returns((true, masterId));

        var act = async () => await _service.Add(new OperateBlacklistLink { GameId = gameId, Username = "Author" });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden && e.Message == RefusalMessage.CannotBlacklistGameLead);
    }

    [Fact]
    public async Task CancelInvitationsWhenAddingToBlacklist()
    {
        var gameId = Guid.NewGuid();
        var username = "testuser";
        var userId = Guid.NewGuid();
        var tokenId = Guid.NewGuid();
        var game = CreateGame(gameId);
        _gameService.GetAsync(gameId).Returns(game);
        _userLookupService.FindUserIdAsync(username, Arg.Any<CancellationToken>()).Returns((true, userId));
        _repository.Add(gameId, userId, _currentUserId).Returns(new GeneralUser { UserId = userId });
        _invitationRepository.CancelInvitationsForUser(gameId, userId, Arg.Any<CancellationToken>())
            .Returns(new[] { new CancelledInvitation { TokenId = tokenId, TokenType = TokenType.GamePlayerInvitation } });

        await _service.Add(new OperateBlacklistLink { GameId = gameId, Username = username });

        await _producer.Received(1).SendAsync(EventType.PlayerInvitationCancelled, tokenId);
        await _characterRepository.Received(1).DeclinePendingCharacters(gameId, userId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishGameChangedEventAfterBlacklisting()
    {
        var gameId = Guid.NewGuid();
        var username = "testuser";
        var userId = Guid.NewGuid();
        var game = CreateGame(gameId);
        _gameService.GetAsync(gameId).Returns(game);
        _userLookupService.FindUserIdAsync(username, Arg.Any<CancellationToken>()).Returns((true, userId));
        _repository.Add(gameId, userId, _currentUserId).Returns(new GeneralUser { UserId = userId });
        _invitationRepository.CancelInvitationsForUser(gameId, userId, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<CancelledInvitation>());

        await _service.Add(new OperateBlacklistLink { GameId = gameId, Username = username });

        await _producer.Received(1).SendAsync(EventType.ChangedGame, gameId);
    }
}
