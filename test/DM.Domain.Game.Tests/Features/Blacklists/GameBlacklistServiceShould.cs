using System;
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
using AwesomeAssertions;
using FluentValidation;
using FluentValidation.Results;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace DM.Domain.Game.Tests.Features.Blacklists;

public class GameBlacklistServiceShould : UnitTestBase
{
    private readonly IGameService gameService;
    private readonly IIntentionManager intentionManager;
    private readonly IIdentityProvider identityProvider;
    private readonly IUserLookupService userLookupService;
    private readonly IGameBlacklistRepository repository;
    private readonly IGameInvitationRepository invitationRepository;
    private readonly ICharacterRepository characterRepository;
    private readonly IEventProducer producer;
    private readonly GameBlacklistService service;
    private readonly Guid currentUserId;

    public GameBlacklistServiceShould()
    {
        var validator = Mock<IValidator<OperateBlacklistLink>>();
        validator.ValidateAsync(Arg.Any<ValidationContext<OperateBlacklistLink>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        gameService = Mock<IGameService>();
        intentionManager = Mock<IIntentionManager>();

        currentUserId = Guid.NewGuid();
        identityProvider = Mock<IIdentityProvider>();
        identityProvider.Current.Returns(Identities.User(currentUserId, UserRole.RegularUser));

        userLookupService = Mock<IUserLookupService>();
        repository = Mock<IGameBlacklistRepository>();
        invitationRepository = Mock<IGameInvitationRepository>();
        characterRepository = Mock<ICharacterRepository>();
        producer = Mock<IEventProducer>();
        producer.SendAsync(Arg.Any<EventType>(), Arg.Any<Guid>()).Returns(Task.CompletedTask);

        service = new GameBlacklistService(
            validator,
            gameService,
            intentionManager,
            identityProvider,
            userLookupService,
            repository,
            invitationRepository,
            characterRepository,
            producer);
    }

    private static GameDetails CreateGame(Guid gameId, BlacklistedUser[]? blacklistedUsers = null) => new GameDetails
    {
        Id = gameId,
        Master = new GeneralUser { UserId = Guid.NewGuid(), Username = "Author" },
        BlacklistedUsers = blacklistedUsers ?? []
    };

    private void ArrangeSuccessfulAdd(Guid gameId, string username, Guid userId, params CancelledInvitation[] cancelled)
    {
        gameService.GetAsync(gameId).Returns(CreateGame(gameId));
        userLookupService.FindUserIdAsync(username, Arg.Any<CancellationToken>()).Returns((true, userId));
        repository.Add(gameId, userId, currentUserId).Returns(new GeneralUser { UserId = userId });
        invitationRepository.CancelInvitationsForUser(gameId, userId, Arg.Any<CancellationToken>()).Returns(cancelled);
    }

    [Fact]
    public async Task AuthorizeGetBlacklist()
    {
        var gameId = Guid.NewGuid();
        var game = CreateGame(gameId);
        gameService.GetAsync(gameId).Returns(game);
        repository.Get(gameId).Returns(Array.Empty<GeneralUser>());

        await service.Get(gameId);

        intentionManager.Received(1).ThrowIfForbidden(GameIntention.Edit, Arg.Is<GameDto>(g => g.Id == gameId));
    }

    [Fact]
    public async Task AuthorizeAddToBlacklist()
    {
        var gameId = Guid.NewGuid();
        ArrangeSuccessfulAdd(gameId, "testuser", Guid.NewGuid());

        await service.Add(new OperateBlacklistLink { GameId = gameId, Username = "testuser" });

        intentionManager.Received(1).ThrowIfForbidden(GameIntention.Edit, Arg.Is<GameDto>(g => g.Id == gameId));
    }

    [Fact]
    public async Task ThrowConflictWhenUserAlreadyBlacklisted()
    {
        var gameId = Guid.NewGuid();
        var username = "testuser";
        var userId = Guid.NewGuid();
        var game = CreateGame(gameId, new[] { new BlacklistedUser { UserId = userId } });
        gameService.GetAsync(gameId).Returns(game);
        userLookupService.FindUserIdAsync(username, Arg.Any<CancellationToken>()).Returns((true, userId));

        var act = async () => await service.Add(new OperateBlacklistLink { GameId = gameId, Username = username });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Conflict);
    }

    // Two requests for one user both pass the read, and the unique index refuses the second insert.
    [Fact]
    public async Task AnswerConflictWhenTheInsertLosesTheRace()
    {
        var gameId = Guid.NewGuid();
        var username = "testuser";
        var userId = Guid.NewGuid();
        var game = CreateGame(gameId);
        gameService.GetAsync(gameId).Returns(game);
        userLookupService.FindUserIdAsync(username, Arg.Any<CancellationToken>()).Returns((true, userId));
        repository.Add(gameId, userId, currentUserId)
            .ThrowsAsync(new DuplicateEntityException("Duplicate game blacklist entry"));

        var act = async () => await service.Add(new OperateBlacklistLink { GameId = gameId, Username = username });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Conflict &&
                        e.Message == RefusalMessage.AlreadyInBlacklist);
        await producer.DidNotReceive().SendAsync(EventType.ChangedGame, gameId);
    }

    // A subscriber keeps the subscription: the service holds no subscription store to end it with.
    [Fact]
    public async Task AddTheUserToTheBlacklist()
    {
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        ArrangeSuccessfulAdd(gameId, "reader", userId);

        await service.Add(new OperateBlacklistLink { GameId = gameId, Username = "reader" });

        await repository.Received(1).Add(gameId, userId, currentUserId);
    }

    [Fact]
    public async Task RefuseToBlacklistAPlayerBeforeTheyLeave()
    {
        var gameId = Guid.NewGuid();
        var username = "player";
        var userId = Guid.NewGuid();
        var game = CreateGame(gameId);
        game.Players = [new GeneralUser { UserId = userId, Username = username }];
        gameService.GetAsync(gameId).Returns(game);
        userLookupService.FindUserIdAsync(username, Arg.Any<CancellationToken>()).Returns((true, userId));

        var act = async () => await service.Add(new OperateBlacklistLink { GameId = gameId, Username = username });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Conflict && e.Message == RefusalMessage.RemoveFromGameFirst);
        await repository.DidNotReceive().Add(gameId, userId, currentUserId);
    }

    [Fact]
    public async Task RefuseToBlacklistTheMasterInTheSharedWords()
    {
        var gameId = Guid.NewGuid();
        var game = CreateGame(gameId);
        var masterId = game.Master!.UserId;
        gameService.GetAsync(gameId).Returns(game);
        userLookupService.FindUserIdAsync("Author", Arg.Any<CancellationToken>()).Returns((true, masterId));

        var act = async () => await service.Add(new OperateBlacklistLink { GameId = gameId, Username = "Author" });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden && e.Message == RefusalMessage.CannotBlacklistGameLead);
    }

    [Fact]
    public async Task CancelInvitationsWhenAddingToBlacklist()
    {
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var tokenId = Guid.NewGuid();
        ArrangeSuccessfulAdd(gameId, "testuser", userId,
            new CancelledInvitation { TokenId = tokenId, TokenType = TokenType.GamePlayerInvitation });

        await service.Add(new OperateBlacklistLink { GameId = gameId, Username = "testuser" });

        await producer.Received(1).SendAsync(EventType.PlayerInvitationCancelled, tokenId);
        await characterRepository.Received(1).DeclinePendingCharacters(gameId, userId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishGameChangedEventAfterBlacklisting()
    {
        var gameId = Guid.NewGuid();
        ArrangeSuccessfulAdd(gameId, "testuser", Guid.NewGuid());

        await service.Add(new OperateBlacklistLink { GameId = gameId, Username = "testuser" });

        await producer.Received(1).SendAsync(EventType.ChangedGame, gameId);
    }
}
