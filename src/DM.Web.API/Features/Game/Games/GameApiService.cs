using System;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Game.Features.Games;
using DM.Web.API.Shared.Dto;
using DomainGamesQuery = DM.Domain.Game.Features.Games.GamesQuery;

namespace DM.Web.API.Features.Game.Games;

/// <inheritdoc />
internal class GameApiService : IGameApiService
{
    private readonly IGameService _gameService;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public GameApiService(
        IGameService gameService,
        IMapper mapper)
    {
        _gameService = gameService;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<Game>> Get(GamesQuery gamesQuery)
    {
        var query = _mapper.Map<DomainGamesQuery>(gamesQuery);
        var (games, paging) = await _gameService.GetGamesAsync(query);
        return new ListEnvelope<Game>(games.Select(_mapper.Map<Game>), new PagingInfo(paging));
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<Game>> GetOwn()
    {
        var games = await _gameService.GetOwnGamesAsync();
        return new ListEnvelope<Game>(games.Select(_mapper.Map<Game>));
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<Game>> GetPopular()
    {
        var games = await _gameService.GetPopularAsync();
        return new ListEnvelope<Game>(games.Select(_mapper.Map<Game>));
    }

    /// <inheritdoc />
    public async Task<Envelope<Game>> Get(Guid gameId)
    {
        var game = await _gameService.GetAsync(gameId);
        return new Envelope<Game>(_mapper.Map<Game>(game));
    }

    /// <inheritdoc />
    public async Task<Envelope<GameDetails>> GetDetails(Guid gameId)
    {
        var game = await _gameService.GetDetailsAsync(gameId);
        return new Envelope<GameDetails>(_mapper.Map<GameDetails>(game));
    }

    /// <inheritdoc />
    public async Task<Envelope<GameDetails>> Create(CreateGameRequest request)
    {
        var createGame = _mapper.Map<CreateGame>(request);
        var createdGame = await _gameService.CreateAsync(createGame);
        return new Envelope<GameDetails>(_mapper.Map<GameDetails>(createdGame));
    }

    /// <inheritdoc />
    public async Task<Envelope<GameDetails>> Update(Guid gameId, GameDetails game)
    {
        var updateGame = _mapper.Map<UpdateGame>(game);
        updateGame.GameId = gameId;
        var updatedGame = await _gameService.UpdateAsync(updateGame);
        return new Envelope<GameDetails>(_mapper.Map<GameDetails>(updatedGame));
    }

    /// <inheritdoc />
    public Task Delete(Guid gameId) => _gameService.DeleteAsync(gameId);

    /// <inheritdoc />
    public async Task<ListEnvelope<Tag>> GetTags()
    {
        var tags = await _gameService.GetTagsAsync();
        return new ListEnvelope<Tag>(tags.Select(_mapper.Map<Tag>));
    }

    /// <inheritdoc />
    public async Task<Envelope<GameNotes>> GetNotes(Guid gameId)
    {
        var game = await _gameService.GetDetailsAsync(gameId);
        return new Envelope<GameNotes>(new GameNotes { Notes = game.Notepad });
    }

    /// <inheritdoc />
    public Task<Envelope<GameNotes>> UpdateNotes(Guid gameId, GameNotes notes)
    {
        // Legacy single-field notepad is deprecated. Use structured notepad API instead.
        // GET /games/{id}/notepad - list entries
        // POST /games/{id}/notepad - create entry
        throw new InvalidOperationException(
            "Legacy game notes endpoint is deprecated. Use the structured notepad API: " +
            $"GET /v1/games/{gameId}/notepad to list entries, " +
            $"POST /v1/games/{gameId}/notepad to create entries.");
    }
}
