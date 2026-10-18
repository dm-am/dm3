using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Game.Features.Comments;
using DM.Domain.Game.Features.GameAttributeSchemas;
using DM.Domain.Game.Features.GameReviews;
using DM.Domain.Game.Features.Games;
using DM.Web.API.Shared.Dto;
using DomainGameDetails = DM.Domain.Game.Features.Games.GameDetails;

namespace DM.Web.API.Features.Game.Games;

/// <inheritdoc />
internal class GameApiService : IGameApiService
{
    private readonly IGameService _gameService;
    private readonly IGameAttributeSchemaService _gameSchemaService;
    private readonly IGameCommentService _commentService;
    private readonly IGameReviewService _reviewService;
    private readonly GameMapper _mapper;

    /// <inheritdoc />
    public GameApiService(
        IGameService gameService,
        IGameAttributeSchemaService gameSchemaService,
        IGameCommentService commentService,
        IGameReviewService reviewService,
        GameMapper mapper)
    {
        _gameService = gameService;
        _gameSchemaService = gameSchemaService;
        _commentService = commentService;
        _reviewService = reviewService;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<Game>> Get(GamesQuery gamesQuery)
    {
        var query = _mapper.ToGamesQuery(gamesQuery);
        var (games, paging) = await _gameService.GetGamesAsync(query);

        // Map and clear Tags (use TagIds + cached /games/tags for descriptions)
        var mappedGames = games.Select(g =>
        {
            var mapped = _mapper.ToGame(g);
            mapped.Tags = null; // Payload optimization: 58% smaller without embedded tag descriptions
            return mapped;
        });

        return new ListEnvelope<Game>(mappedGames, new PagingInfo(paging));
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<GameRef>> GetRefs(GamesQuery gamesQuery)
    {
        var query = _mapper.ToGamesQuery(gamesQuery);
        var (games, paging) = await _gameService.GetGamesAsync(query);
        return new ListEnvelope<GameRef>(games.Select(_mapper.ToGameRef), new PagingInfo(paging));
    }

    /// <inheritdoc />
    public async Task<Envelope<Game>> Get(Guid gameId)
    {
        var game = await _gameService.GetAsync(gameId);
        return new Envelope<Game>(_mapper.ToGame(game));
    }

    /// <inheritdoc />
    public async Task<Envelope<Game>> GetByPublicId(string publicId)
    {
        var game = await _gameService.GetByPublicIdAsync(publicId);
        return new Envelope<Game>(_mapper.ToGame(game));
    }

    /// <inheritdoc />
    public async Task<Envelope<GameDetails>> GetDetails(Guid gameId)
    {
        var game = await _gameService.GetDetailsAsync(gameId);
        return new Envelope<GameDetails>(await DetailsForViewer(game));
    }

    /// <inheritdoc />
    public async Task<Envelope<GameDetails>> GetDetailsByPublicId(string publicId)
    {
        var game = await _gameService.GetDetailsByPublicIdAsync(publicId);
        return new Envelope<GameDetails>(await DetailsForViewer(game));
    }

    /// <summary>
    /// The details of a game as the viewer gets them: the game, and what he may
    /// do in it. Every answer that carries the details goes through here, the
    /// writes included, because a page replaces the game it holds with the one
    /// a write returns.
    /// </summary>
    private async Task<GameDetails> DetailsForViewer(DomainGameDetails game)
    {
        var details = _mapper.ToGameDetails(game);
        details.Viewer = new GameViewer
        {
            CanComment = _commentService.MayCreate(game),
            CanReview = await _reviewService.MayCreateAsync(game.Id)
        };
        return details;
    }

    /// <inheritdoc />
    public async Task<Guid> ResolveId(string idOrPublicId) =>
        Guid.TryParse(idOrPublicId, out var guid)
            ? guid
            : await _gameService.ResolveIdByPublicIdAsync(idOrPublicId);

    /// <inheritdoc />
    public async Task<Envelope<GameDetails>> Create(CreateGameRequest request)
    {
        var createGame = _mapper.ToCreateGame(request);
        var createdGame = await _gameService.CreateAsync(createGame);
        return new Envelope<GameDetails>(await DetailsForViewer(createdGame));
    }

    /// <inheritdoc />
    public async Task<Envelope<GameDetails>> Update(Guid gameId, UpdateGameRequest request)
    {
        var updateGame = _mapper.ToUpdateGame(request);
        updateGame.GameId = gameId;
        var updatedGame = await _gameService.UpdateAsync(updateGame);
        return new Envelope<GameDetails>(await DetailsForViewer(updatedGame));
    }

    /// <inheritdoc />
    public async Task<Envelope<GameDetails>> ChangeStatus(Guid gameId, GameStatusChangeRequest request)
    {
        var updatedGame = await _gameService.ChangeStatusAsync(gameId, request.Transition);
        return new Envelope<GameDetails>(await DetailsForViewer(updatedGame));
    }

    /// <inheritdoc />
    public async Task<Envelope<GameDetails>> ChangePremoderation(string id, GamePremoderationChangeRequest request)
    {
        var updatedGame = await _gameService.ChangePremoderationAsync(id, request.Transition);
        return new Envelope<GameDetails>(await DetailsForViewer(updatedGame));
    }

    /// <inheritdoc />
    public async Task<Envelope<GameDetails>> ResetRecruitmentDate(Guid gameId)
    {
        var updatedGame = await _gameService.ResetRecruitmentDateAsync(gameId);
        return new Envelope<GameDetails>(await DetailsForViewer(updatedGame));
    }

    /// <inheritdoc />
    public async Task<Envelope<GameAttributeSchema>> ReplaceAttributeSchema(
        Guid gameId, ReplaceGameAttributeSchemaRequest request)
    {
        var replace = _mapper.ToReplaceGameAttributeSchema(request);
        replace.GameId = gameId;
        var replaced = await _gameSchemaService.ReplaceAsync(replace);
        return new Envelope<GameAttributeSchema>(_mapper.ToGameAttributeSchema(replaced));
    }

    /// <inheritdoc />
    public Task Delete(Guid gameId) => _gameService.DeleteAsync(gameId);

    /// <inheritdoc />
    public async Task<ListEnvelope<Tag>> GetTags()
    {
        var tags = await _gameService.GetTagsAsync();
        return new ListEnvelope<Tag>(tags.Select(_mapper.ToTag));
    }
}
