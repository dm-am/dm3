using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Users;
using DM.Domain.Game.Features.GameReviews;
using DM.Domain.Game.Features.Games;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Game.Reviews;

/// <inheritdoc />
internal class GameReviewApiService : IGameReviewApiService
{
    private readonly IGameReviewService _gameReviewService;
    private readonly IUserLookupService _userLookupService;
    private readonly ReviewMapper _mapper;

    /// <inheritdoc />
    public GameReviewApiService(
        IGameReviewService gameReviewService,
        IUserLookupService userLookupService,
        ReviewMapper mapper)
    {
        _gameReviewService = gameReviewService;
        _userLookupService = userLookupService;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<GameReviewDto>> GetList(Guid gameId, PagingQuery query)
    {
        var (reviews, paging) = await _gameReviewService.GetListAsync(gameId, query);
        var apiReviews = reviews.Select(_mapper.ToGameReview);
        return new ListEnvelope<GameReviewDto>(apiReviews, new PagingInfo(paging));
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<GameReviewDto>> GetReceivedByUser(string username, UserGameReviewsQuery query)
    {
        var user = await _userLookupService.GetAsync(username);
        return await GetFiltered(query, Filter(query, f => f.GmId = user.UserId));
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<GameReviewDto>> GetWrittenByUser(string username, UserGameReviewsQuery query)
    {
        var user = await _userLookupService.GetAsync(username);
        return await GetFiltered(query, Filter(query, f => f.AuthorId = user.UserId));
    }

    /// <summary>
    /// Search and sorting come from the query as they are; the caller adds
    /// the one field that says WHOSE reviews these are. Written out here so
    /// the two listings cannot drift apart on what they forward.
    /// </summary>
    private static GameReviewFilter Filter(UserGameReviewsQuery query, Action<GameReviewFilter> scope)
    {
        var filter = new GameReviewFilter
        {
            Search = query.Search,
            SortBy = query.SortBy,
            SortOrder = query.SortOrder
        };
        scope(filter);
        return filter;
    }

    private async Task<ListEnvelope<GameReviewDto>> GetFiltered(PagingQuery query, GameReviewFilter filter)
    {
        var (reviews, paging) = await _gameReviewService.GetAllAsync(query, filter);
        var apiReviews = reviews.Select(_mapper.ToGameReview);
        return new ListEnvelope<GameReviewDto>(apiReviews, new PagingInfo(paging));
    }

    /// <inheritdoc />
    public async Task<Envelope<GameReviewDto>> Get(Guid reviewId)
    {
        var review = await _gameReviewService.GetAsync(reviewId);
        return new Envelope<GameReviewDto>(_mapper.ToGameReview(review));
    }

    /// <inheritdoc />
    public async Task<Envelope<GameReviewDto>> Create(Guid gameId, CreateGameReviewRequest request)
    {
        var createReview = new CreateGameReview
        {
            GameId = gameId,
            Text = request.Text
        };
        var review = await _gameReviewService.CreateAsync(createReview);
        return new Envelope<GameReviewDto>(_mapper.ToGameReview(review));
    }
}
