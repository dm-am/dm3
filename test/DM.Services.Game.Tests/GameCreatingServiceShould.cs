using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Services.Authentication.Dto;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Core.Dto.Enums;
using DM.Services.Game.BusinessProcesses.Games.AssistantAssignment;
using DM.Services.Game.BusinessProcesses.Games.Creating;
using DM.Services.Game.BusinessProcesses.Games.Creating.Facades;
using DM.Services.Game.Dto.Input;
using DM.Services.Game.Dto.Output;
using DM.Tests.Core;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Language.Flow;
using Xunit;
using DbGame = DM.Services.DataAccess.BusinessObjects.Games.Game;
using DbRoom = DM.Services.DataAccess.BusinessObjects.Games.Posts.Room;
using DbGameTag = DM.Services.DataAccess.BusinessObjects.Games.Links.GameTag;

namespace DM.Services.Game.Tests;

public class GameCreatingServiceShould : UnitTestBase
{
    private readonly ISetup<IIdentity, AuthenticatedUser> currentUserSetup;
    private readonly ISetup<IGameEntityFactory, DbGame> createGameSetup;
    private readonly ISetup<IGameEntityFactory, DbRoom> createRoomSetup;
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
            .Returns((ModuleStatus.Active, PremoderationStatus.Approved, true));

        entityFactory = Mock<IGameEntityFactory>();
        createGameSetup = entityFactory
            .Setup(f => f.CreateGame(It.IsAny<CreateGame>(), It.IsAny<Guid>(), It.IsAny<ModuleStatus>(),
                It.IsAny<PremoderationStatus>(), It.IsAny<bool>()));
        createRoomSetup = entityFactory.Setup(f => f.CreateDefaultRoom(It.IsAny<Guid>()));
        entityFactory
            .Setup(f => f.CreateTags(It.IsAny<Guid>(), It.IsAny<IEnumerable<Guid>>()))
            .Returns(Array.Empty<DbGameTag>());

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
            .Setup(r => r.Create(It.IsAny<DbGame>(), It.IsAny<DbRoom>(),
                It.IsAny<IEnumerable<DbGameTag>>()));

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
        createGameSetup.Returns(new DbGame());
        createRoomSetup.Returns(new DbRoom());
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
        createGameSetup.Returns(new DbGame());
        createRoomSetup.Returns(new DbRoom());
        saveGameSetup.ReturnsAsync(new GameExtended());

        validator
            .Setup(v => v.GetInitialGameState(It.IsAny<CreateGame>()))
            .Returns((ModuleStatus.Active, PremoderationStatus.AwaitingApproval, false));

        var createGame = new CreateGame();
        await service.Create(createGame);

        entityFactory.Verify(f => f.CreateGame(createGame, userId, ModuleStatus.Active,
            PremoderationStatus.AwaitingApproval, false));
    }

    [Fact]
    public async Task SearchForAssistantWhenLoginGiven()
    {
        currentUserSetup.Returns(new AuthenticatedUser());
        var game = new DbGame();
        var room = new DbRoom();
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
        var game = new DbGame { GameId = gameId };
        var room = new DbRoom();
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
        var game = new DbGame();
        var room = new DbRoom();
        createGameSetup.Returns(game);
        createRoomSetup.Returns(room);
        saveGameSetup.ReturnsAsync(new GameExtended());

        await service.Create(new CreateGame());

        gameRepository.Verify(r => r.Create(game, room, It.IsAny<IEnumerable<DbGameTag>>()), Times.Once);
    }

    [Fact]
    public async Task InitializeCountersAndPublishEvent()
    {
        currentUserSetup.Returns(new AuthenticatedUser());
        var gameId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        createGameSetup.Returns(new DbGame { GameId = gameId });
        createRoomSetup.Returns(new DbRoom { RoomId = roomId });
        saveGameSetup.ReturnsAsync(new GameExtended());

        await service.Create(new CreateGame());

        initialization.Verify(i => i.InitializeCounters(gameId, roomId), Times.Once);
        initialization.Verify(i => i.PublishGameCreated(gameId), Times.Once);
    }
}
