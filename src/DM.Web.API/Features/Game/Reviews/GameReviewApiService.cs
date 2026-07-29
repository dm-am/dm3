using System;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Core.Dto;
using DM.Domain.Game.Features.GameReviews;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Game.Reviews;

/// <inheritdoc />
internal class GameReviewApiService : IGameReviewApiService
{
    private readonly IGameReviewService _gameReviewService;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public GameReviewApiService(
        IGameReviewService gameReviewService,
        IMapper mapper)
    {
        _gameReviewService = gameReviewService;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<GameReviewDto>> GetList(Guid gameId, PagingQuery query)
    {
        var (reviews, paging) = await _gameReviewService.GetListAsync(gameId, query);
        var apiReviews = reviews.Select(_mapper.Map<GameReviewDto>);
        return new ListEnvelope<GameReviewDto>(apiReviews, new PagingInfo(paging));
    }

    /// <inheritdoc />
    public async Task<Envelope<GameReviewDto>> Get(Guid reviewId)
    {
        var review = await _gameReviewService.GetAsync(reviewId);
        return new Envelope<GameReviewDto>(_mapper.Map<GameReviewDto>(review));
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
        return new Envelope<GameReviewDto>(_mapper.Map<GameReviewDto>(review));
    }
}
