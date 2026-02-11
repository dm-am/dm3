using System;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Services.Game.BusinessProcesses.Games.Creating;
using DM.Services.Game.BusinessProcesses.Games.Deleting;
using DM.Services.Game.BusinessProcesses.Games.Reading;
using DM.Services.Game.BusinessProcesses.Games.Updating;
using DM.Services.Game.Dto.Input;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Dto.Games;
using GameApiQuery = DM.Web.API.Dto.Games.GamesQuery;
using GamesQuery = DM.Services.Game.Dto.Input.GamesQuery;

namespace DM.Web.API.Services.Game;

/// <inheritdoc />
internal class GameApiService : IGameApiService
{
    private readonly IGameReadingService readingService;
    private readonly IGameCreatingService creatingService;
    private readonly IGameUpdatingService updatingService;
    private readonly IGameDeletingService deletingService;
    private readonly IMapper mapper;

    /// <inheritdoc />
    public GameApiService(
        IGameReadingService readingService,
        IGameCreatingService creatingService,
        IGameUpdatingService updatingService,
        IGameDeletingService deletingService,
        IMapper mapper)
    {
        this.readingService = readingService;
        this.creatingService = creatingService;
        this.updatingService = updatingService;
        this.deletingService = deletingService;
        this.mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<Dto.Games.Game>> Get(GameApiQuery gamesQuery)
    {
        var query = mapper.Map<GamesQuery>(gamesQuery);
        var (games, paging) = await readingService.GetGames(query);
        return new ListEnvelope<Dto.Games.Game>(games.Select(mapper.Map<Dto.Games.Game>), new Paging(paging));
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<Dto.Games.Game>> GetOwn()
    {
        var games = await readingService.GetOwnGames();
        return new ListEnvelope<Dto.Games.Game>(games.Select(mapper.Map<Dto.Games.Game>));
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<Dto.Games.Game>> GetPopular()
    {
        var games = await readingService.GetPopularGames();
        return new ListEnvelope<Dto.Games.Game>(games.Select(mapper.Map<Dto.Games.Game>));
    }

    /// <inheritdoc />
    public async Task<Envelope<Dto.Games.Game>> Get(Guid gameId)
    {
        var game = await readingService.GetGame(gameId);
        return new Envelope<Dto.Games.Game>(mapper.Map<Dto.Games.Game>(game));
    }

    /// <inheritdoc />
    public async Task<Envelope<GameDetails>> GetDetails(Guid gameId)
    {
        var game = await readingService.GetGameDetails(gameId);
        return new Envelope<GameDetails>(mapper.Map<GameDetails>(game));
    }

    /// <inheritdoc />
    public async Task<Envelope<GameDetails>> Create(CreateGameRequest request)
    {
        var createGame = mapper.Map<CreateGame>(request);
        var createdGame = await creatingService.Create(createGame);
        return new Envelope<GameDetails>(mapper.Map<GameDetails>(createdGame));
    }

    /// <inheritdoc />
    public async Task<Envelope<GameDetails>> Update(Guid gameId, GameDetails game)
    {
        var updateGame = mapper.Map<UpdateGame>(game);
        updateGame.GameId = gameId;
        var updatedGame = await updatingService.Update(updateGame);
        return new Envelope<GameDetails>(mapper.Map<GameDetails>(updatedGame));
    }

    /// <inheritdoc />
    public Task Delete(Guid gameId) => deletingService.DeleteGame(gameId);

    /// <inheritdoc />
    public async Task<ListEnvelope<Tag>> GetTags()
    {
        var tags = await readingService.GetTags();
        return new ListEnvelope<Tag>(tags.Select(mapper.Map<Tag>));
    }

    /// <inheritdoc />
    public async Task<Envelope<GameNotes>> GetNotes(Guid gameId)
    {
        var game = await readingService.GetGameDetails(gameId);
        return new Envelope<GameNotes>(new GameNotes { Notes = game.Notepad });
    }

    /// <inheritdoc />
    public async Task<Envelope<GameNotes>> UpdateNotes(Guid gameId, GameNotes notes)
    {
        var updateGame = new UpdateGame
        {
            GameId = gameId,
            Notepad = notes.Notes
        };
        var updatedGame = await updatingService.Update(updateGame);
        return new Envelope<GameNotes>(new GameNotes { Notes = updatedGame.Notepad });
    }
}
