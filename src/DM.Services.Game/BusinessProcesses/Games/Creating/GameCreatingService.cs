using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Game.BusinessProcesses.Games.AssistantAssignment;
using DM.Services.Game.BusinessProcesses.Games.Creating.Facades;
using DM.Services.Game.Dto.Input;
using DM.Services.Game.Dto.Output;
using Microsoft.Extensions.Logging;
using DbTag = DM.Services.DataAccess.BusinessObjects.Games.Links.GameTag;

namespace DM.Services.Game.BusinessProcesses.Games.Creating;

/// <inheritdoc />
internal class GameCreatingService : IGameCreatingService
{
    private readonly IGameCreationValidator _validator;
    private readonly IGameEntityFactory _entityFactory;
    private readonly IGameCreationDataResolver _dataResolver;
    private readonly IGameInitializationService _initialization;
    private readonly IGameCreatingRepository _repository;
    private readonly IAssignmentService _assignmentService;
    private readonly IIdentityProvider _identityProvider;
    private readonly ILogger<GameCreatingService> _logger;

    /// <inheritdoc />
    public GameCreatingService(
        IGameCreationValidator validator,
        IGameEntityFactory entityFactory,
        IGameCreationDataResolver dataResolver,
        IGameInitializationService initialization,
        IGameCreatingRepository repository,
        IAssignmentService assignmentService,
        IIdentityProvider identityProvider,
        ILogger<GameCreatingService> logger)
    {
        _validator = validator;
        _entityFactory = entityFactory;
        _dataResolver = dataResolver;
        _initialization = initialization;
        _repository = repository;
        _assignmentService = assignmentService;
        _identityProvider = identityProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<GameExtended> Create(CreateGame createGame)
    {
        _logger.LogDebug("Creating game. Title={Title}", createGame.Title);

        // Validate and authorize
        await _validator.ValidateAndAuthorize(createGame);

        // Resolve game initial status and premoderation
        var (initialStatus, premoderationStatus, isRecruitmentOpen) = _validator.GetInitialGameState(createGame);

        // Create base DAL entities
        var userId = _identityProvider.Current.User.UserId;
        var game = _entityFactory.CreateGame(createGame, userId, initialStatus, premoderationStatus, isRecruitmentOpen);
        var room = _entityFactory.CreateDefaultRoom(game.GameId);

        // Create tags
        IEnumerable<DbTag> tags;
        if (createGame.Tags != null && createGame.Tags.Any())
        {
            var availableTags = (await _dataResolver.GetAvailableTagIds()).ToHashSet();
            var validTagIds = createGame.Tags.Where(availableTags.Contains);
            tags = _entityFactory.CreateTags(game.GameId, validTagIds);
        }
        else
        {
            tags = Enumerable.Empty<DbTag>();
        }

        // Initiate assistant assignment
        if (!string.IsNullOrEmpty(createGame.AssistantLogin))
        {
            var (assistantExists, assistantId) = await _dataResolver.FindAssistantId(createGame.AssistantLogin);
            if (assistantExists)
            {
                await _assignmentService.CreateAssignment(game.GameId, assistantId);
            }
        }

        // Apply attribute schema if allowed
        if (createGame.AttributeSchemaId.HasValue)
        {
            var allowedSchemaId = await _dataResolver.GetAllowedSchemaId(createGame.AttributeSchemaId.Value);
            if (allowedSchemaId.HasValue)
            {
                game.AttributeSchemaId = allowedSchemaId.Value;
            }
        }

        // Persist the game
        var createdGame = await _repository.Create(game, room, tags);

        // Initialize counters and publish event
        await _initialization.InitializeCounters(game.GameId, room.RoomId);
        await _initialization.PublishGameCreated(game.GameId);

        _logger.LogInformation("Game created successfully. GameId={GameId}, Title={Title}, MasterId={MasterId}",
            game.GameId, createGame.Title, userId);

        return createdGame;
    }
}