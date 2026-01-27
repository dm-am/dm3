using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Services.Authentication.Dto;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Core.Dto.Enums;
using DM.Services.Gaming.BusinessProcesses.Games.AssistantAssignment;
using DM.Services.Gaming.BusinessProcesses.Games.Creating;
using DM.Services.Gaming.BusinessProcesses.Games.Creating.Facades;
using DM.Services.Gaming.Dto.Input;
using DM.Services.Gaming.Dto.Output;
using DM.Tests.Core;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Language.Flow;
using Xunit;
using Game = DM.Services.DataAccess.BusinessObjects.Games.Game;
using Room = DM.Services.DataAccess.BusinessObjects.Games.Posts.Room;
using GameTag = DM.Services.DataAccess.BusinessObjects.Games.Links.GameTag;

namespace DM.Services.Gaming.Tests;

public class GameCreatingServiceShould : UnitTestBase
{
    private readonly ISetup<IIdentity, AuthenticatedUser> currentUserSetup;
    private readonly ISetup<IGameEntityFactory, Game> createGameSetup;
    private readonly ISetup<IGameEntityFactory, Room> createRoomSetup;
    private readonly ISetup<IGameCreatingRepository, Task<GameExtended>> saveGameSetup;
    private readonly Mock<IGameCreatingRepository> gameRepository;
    private readonly Mock<IGameEntityFactory> entityFactory;
    private readonly Mock<IGameCreationValidator> validator;
    private readonly Mock<IGameInitializationService> initialization;
    private readonly GameCreatingService service;
    private readonly Mock<IAssignmentService> assignmentService;
    private readonly Mock<IGameCreationDataResolver> dataResolver;

    public GameCreatingServiceShould()
    {
        validator = Mock<IGameCreationValidator>();
        validator
            .Setup(v => v.ValidateAndAuthorize(It.IsAny<CreateGame>()))
            .Returns(Task.CompletedTask);
        validator
            .Setup(v => v.GetInitialGameState(It.IsAny<CreateGame>()))
            .Returns((GameStatus.Active, PremoderationStatus.Approved, true));

        entityFactory = Mock<IGameEntityFactory>();
        createGameSetup = entityFactory
            .Setup(f => f.CreateGame(It.IsAny<CreateGame>(), It.IsAny<Guid>(), It.IsAny<GameStatus>(),
                It.IsAny<PremoderationStatus>(), It.IsAny<bool>()));
        createRoomSetup = entityFactory.Setup(f => f.CreateDefaultRoom(It.IsAny<Guid>()));
        entityFactory
            .Setup(f => f.CreateTags(It.IsAny<Guid>(), It.IsAny<IEnumerable<Guid>>()))
            .Returns(Array.Empty<GameTag>());

        dataResolver = Mock<IGameCreationDataResolver>();
        dataResolver
            .Setup(r => r.FindAssistantId(It.IsAny<string>()))
            .ReturnsAsync((false, Guid.Empty));

        initialization = Mock<IGameInitializationService>();
        initialization
            .Setup(i => i.InitializeCounters(It.IsAny<Guid>(), It.IsAny<Guid>()))
            .Returns(Task.CompletedTask);
        initialization
            .Setup(i => i.PublishGameCreated(It.IsAny<Guid>()))
            .Returns(Task.CompletedTask);

        gameRepository = Mock<IGameCreatingRepository>();
        saveGameSetup = gameRepository
            .Setup(r => r.Create(It.IsAny<Game>(), It.IsAny<Room>(),
                It.IsAny<IEnumerable<GameTag>>()));

        assignmentService = Mock<IAssignmentService>();
        assignmentService
            .Setup(s => s.CreateAssignment(It.IsAny<Guid>(), It.IsAny<Guid>()))
            .Returns(Task.CompletedTask);

        var identityProvider = Mock<IIdentityProvider>();
        var identity = Mock<IIdentity>();
        identityProvider.Setup(p => p.Current).Returns(identity.Object);
        currentUserSetup = identity.Setup(i => i.User);
        var logger = Mock<ILogger<GameCreatingService>>();

        service = new GameCreatingService(
            validator.Object,
            entityFactory.Object,
            dataResolver.Object,
            initialization.Object,
            gameRepository.Object,
            assignmentService.Object,
            identityProvider.Object,
            logger.Object);
    }

    [Fact]
    public async Task CheckValidationAndAuthorization()
    {
        currentUserSetup.Returns(new AuthenticatedUser());
        createGameSetup.Returns(new Game());
        createRoomSetup.Returns(new Room());
        saveGameSetup.ReturnsAsync(new GameExtended());

        var createGame = new CreateGame();
        await service.Create(createGame);

        validator.Verify(v => v.ValidateAndAuthorize(createGame), Times.Once);
    }

    [Fact]
    public async Task CreateGameWithCorrectStatus()
    {
        var userId = Guid.NewGuid();
        currentUserSetup.Returns(new AuthenticatedUser { UserId = userId });
        createGameSetup.Returns(new Game());
        createRoomSetup.Returns(new Room());
        saveGameSetup.ReturnsAsync(new GameExtended());

        validator
            .Setup(v => v.GetInitialGameState(It.IsAny<CreateGame>()))
            .Returns((GameStatus.Active, PremoderationStatus.AwaitingApproval, false));

        var createGame = new CreateGame();
        await service.Create(createGame);

        entityFactory.Verify(f => f.CreateGame(createGame, userId, GameStatus.Active,
            PremoderationStatus.AwaitingApproval, false));
    }

    [Fact]
    public async Task SearchForAssistantWhenLoginGiven()
    {
        currentUserSetup.Returns(new AuthenticatedUser());
        var game = new Game();
        var room = new Room();
        createGameSetup.Returns(game);
        createRoomSetup.Returns(room);
        saveGameSetup.ReturnsAsync(new GameExtended());

        var createGame = new CreateGame { AssistantLogin = "assistant boi" };
        await service.Create(createGame);

        dataResolver.Verify(r => r.FindAssistantId("assistant boi"));
    }

    [Fact]
    public async Task CreateGameWithAssistantTokenWhenFound()
    {
        currentUserSetup.Returns(new AuthenticatedUser());
        var gameId = Guid.NewGuid();
        var game = new Game { GameId = gameId };
        var room = new Room();
        createGameSetup.Returns(game);
        createRoomSetup.Returns(room);
        saveGameSetup.ReturnsAsync(new GameExtended());
        var assistantId = Guid.NewGuid();
        dataResolver
            .Setup(r => r.FindAssistantId(It.IsAny<string>()))
            .ReturnsAsync((true, assistantId));

        await service.Create(new CreateGame { AssistantLogin = "assistant boi" });

        assignmentService.Verify(s => s.CreateAssignment(gameId, assistantId), Times.Once);
    }

    [Fact]
    public async Task SaveCreatedGameAndRoomInStorage()
    {
        currentUserSetup.Returns(new AuthenticatedUser());
        var game = new Game();
        var room = new Room();
        createGameSetup.Returns(game);
        createRoomSetup.Returns(room);
        saveGameSetup.ReturnsAsync(new GameExtended());

        await service.Create(new CreateGame());

        gameRepository.Verify(r => r.Create(game, room, It.IsAny<IEnumerable<GameTag>>()), Times.Once);
    }

    [Fact]
    public async Task InitializeCountersAndPublishEvent()
    {
        currentUserSetup.Returns(new AuthenticatedUser());
        var gameId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        createGameSetup.Returns(new Game { GameId = gameId });
        createRoomSetup.Returns(new Room { RoomId = roomId });
        saveGameSetup.ReturnsAsync(new GameExtended());

        await service.Create(new CreateGame());

        initialization.Verify(i => i.InitializeCounters(gameId, roomId), Times.Once);
        initialization.Verify(i => i.PublishGameCreated(gameId), Times.Once);
    }
}
