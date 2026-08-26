using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Users;
using DM.Domain.Game.Features.PostReviews;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Game.Reviews;

/// <inheritdoc />
internal class PostReviewApiService : IPostReviewApiService
{
    private readonly IPostReviewService _postReviewService;
    private readonly IUserLookupService _userLookupService;
    private readonly ReviewMapper _mapper;

    /// <inheritdoc />
    public PostReviewApiService(
        IPostReviewService postReviewService,
        IUserLookupService userLookupService,
        ReviewMapper mapper)
    {
        _postReviewService = postReviewService;
        _userLookupService = userLookupService;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<PostReviewDto>> GetAll(
        PagingQuery query, string? authorUsername, string? recipientUsername, Guid? gameId)
    {
        var filter = new PostReviewFilter
        {
            GameId = gameId
        };

        if (!string.IsNullOrWhiteSpace(authorUsername))
        {
            var author = await _userLookupService.GetAsync(authorUsername);
            filter.AuthorId = author.UserId;
        }

        if (!string.IsNullOrWhiteSpace(recipientUsername))
        {
            var recipient = await _userLookupService.GetAsync(recipientUsername);
            filter.RecipientId = recipient.UserId;
        }

        var (reviews, paging) = await _postReviewService.GetAllAsync(query, filter);
        var apiReviews = reviews.Select(_mapper.ToPostReview);
        return new ListEnvelope<PostReviewDto>(apiReviews, new PagingInfo(paging));
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<PostReviewDto>> GetList(Guid postId, PagingQuery query)
    {
        var (reviews, paging) = await _postReviewService.GetListAsync(postId, query);
        var apiReviews = reviews.Select(_mapper.ToPostReview);
        return new ListEnvelope<PostReviewDto>(apiReviews, new PagingInfo(paging));
    }

    /// <inheritdoc />
    public async Task<Envelope<PostReviewDto>> Get(Guid postId, Guid reviewId)
    {
        var review = await _postReviewService.GetAsync(postId, reviewId);
        return new Envelope<PostReviewDto>(_mapper.ToPostReview(review));
    }

    /// <inheritdoc />
    public async Task<Envelope<PostReviewDto>> Create(Guid postId, CreatePostReviewRequest request)
    {
        var createReview = new CreatePostReview
        {
            PostId = postId,
            Sign = request.Sign,
            Text = request.Text
        };
        var review = await _postReviewService.CreateAsync(createReview);
        return new Envelope<PostReviewDto>(_mapper.ToPostReview(review));
    }

    /// <inheritdoc />
    public async Task<Envelope<PostReviewDto>> Update(Guid reviewId, UpdatePostReviewRequest request)
    {
        var updateReview = new UpdatePostReview
        {
            ReviewId = reviewId,
            Sign = request.Sign,
            Text = request.Text
        };
        var review = await _postReviewService.UpdateAsync(updateReview);
        return new Envelope<PostReviewDto>(_mapper.ToPostReview(review));
    }

    /// <inheritdoc />
    public Task Delete(Guid reviewId) => _postReviewService.DeleteAsync(reviewId);
}
