using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Users;
using DM.Domain.Game.Features.GameReviews;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Game.Reviews;

public class GameReviewApiService
{
    private readonly IGameReviewService _gameReviewService;
    private readonly IUserLookupService _userLookupService;
    private readonly ReviewMapper _mapper;

    public GameReviewApiService(
        IGameReviewService gameReviewService,
        IUserLookupService userLookupService,
        ReviewMapper mapper)
    {
        _gameReviewService = gameReviewService;
        _userLookupService = userLookupService;
        _mapper = mapper;
    }

    public async Task<ListEnvelope<GameReviewDto>> GetList(Guid gameId, PagingQuery query)
    {
        var (reviews, paging) = await _gameReviewService.GetListAsync(gameId, query);
        var apiReviews = reviews.Select(ForViewer);
        return new ListEnvelope<GameReviewDto>(apiReviews, new PagingInfo(paging));
    }

    public async Task<ListEnvelope<GameReviewDto>> GetReceivedByUser(string username, UserGameReviewsQuery query)
    {
        var user = await _userLookupService.GetAsync(username);
        return await GetFiltered(query, Filter(query, gmId: user.UserId));
    }

    public async Task<ListEnvelope<GameReviewDto>> GetWrittenByUser(string username, UserGameReviewsQuery query)
    {
        var user = await _userLookupService.GetAsync(username);
        return await GetFiltered(query, Filter(query, authorId: user.UserId));
    }

    /// <summary>
    /// Search and sorting come from the query as they are; the caller names whose
    /// reviews these are, so the two listings forward the same.
    /// </summary>
    private static GameReviewFilter Filter(UserGameReviewsQuery query, Guid? gmId = null, Guid? authorId = null) => new()
    {
        GmId = gmId,
        AuthorId = authorId,
        Search = query.Search,
        SortBy = query.SortBy,
        SortOrder = query.SortOrder
    };

    private async Task<ListEnvelope<GameReviewDto>> GetFiltered(PagingQuery query, GameReviewFilter filter)
    {
        var (reviews, paging) = await _gameReviewService.GetAllAsync(query, filter);
        var apiReviews = reviews.Select(ForViewer);
        return new ListEnvelope<GameReviewDto>(apiReviews, new PagingInfo(paging));
    }

    public async Task<Envelope<GameReviewDto>> Get(Guid gameId, Guid reviewId)
    {
        var review = await _gameReviewService.GetAsync(gameId, reviewId);
        return new Envelope<GameReviewDto>(ForViewer(review));
    }

    public async Task<Envelope<GameReviewDto>> Create(Guid gameId, CreateGameReviewRequest request)
    {
        var createReview = new CreateGameReview
        {
            GameId = gameId,
            Text = request.Text
        };
        var review = await _gameReviewService.CreateAsync(createReview);
        return new Envelope<GameReviewDto>(ForViewer(review));
    }

    public async Task<Envelope<GameReviewDto>> Update(Guid reviewId, UpdateGameReviewRequest request)
    {
        var updateReview = new UpdateGameReview
        {
            ReviewId = reviewId,
            Text = request.Text
        };
        var review = await _gameReviewService.UpdateAsync(updateReview);
        return new Envelope<GameReviewDto>(ForViewer(review));
    }

    public Task Delete(Guid reviewId) => _gameReviewService.DeleteAsync(reviewId);

    /// <summary>
    /// A review as the viewer gets it: the review, and what he may do with it.
    /// </summary>
    private GameReviewDto ForViewer(GameReview review)
    {
        var result = _mapper.ToGameReview(review);
        result.Viewer = new GameReviewViewer
        {
            CanEdit = _gameReviewService.MayEdit(review),
            CanDelete = _gameReviewService.MayDelete(review)
        };
        return result;
    }
}
