using System;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Services.Community.BusinessProcesses.Reviews.Deleting;
using DM.Services.Community.BusinessProcesses.Reviews.Reading;
using DM.Services.Community.BusinessProcesses.Reviews.Updating;
using DM.Services.Community.BusinessProcesses.Users.Reading;
using DM.Services.Core.Dto;
using DM.Web.API.Authentication;
using DM.Web.API.Dto.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ApiReview = DM.Web.API.Dto.Community.Review;
using UpdateReviewRequest = DM.Web.API.Dto.Community.UpdateReviewRequest;

namespace DM.Web.API.Controllers.v1.Community;

/// <summary>
/// Unified review entity operations - get, update, delete any review by ID
/// </summary>
/// <remarks>
/// This controller handles single review operations regardless of review type
/// (platform, game, or user reviews). For listing and creating reviews,
/// use the type-specific collection endpoints.
/// </remarks>
[ApiController]
[Route("v1/reviews")]
[ApiExplorerSettings(GroupName = "Community")]
[Tags("Reviews")]
public class ReviewController : ControllerBase
{
    private readonly IReviewReadingService _readingService;
    private readonly IReviewUpdatingService _updatingService;
    private readonly IReviewDeletingService _deletingService;
    private readonly IUserReadingService _userReadingService;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public ReviewController(
        IReviewReadingService readingService,
        IReviewUpdatingService updatingService,
        IReviewDeletingService deletingService,
        IUserReadingService userReadingService,
        IMapper mapper)
    {
        _readingService = readingService;
        _updatingService = updatingService;
        _deletingService = deletingService;
        _userReadingService = userReadingService;
        _mapper = mapper;
    }

    /// <summary>
    /// Get all game reviews
    /// </summary>
    /// <remarks>
    /// Returns paginated list of all game reviews across all games.
    /// Supports filtering by author, game, or GM.
    /// </remarks>
    /// <param name="q">Paging parameters</param>
    /// <param name="authorLogin">Filter by review author login</param>
    /// <param name="gmLogin">Filter by games where this user is GM</param>
    /// <param name="gameId">Filter by specific game ID</param>
    /// <response code="200">List of game reviews</response>
    [HttpGet("games", Name = nameof(GetAllGameReviews))]
    [ProducesResponseType(typeof(ListEnvelope<ApiReview>), 200)]
    public async Task<IActionResult> GetAllGameReviews(
        [FromQuery] PagingQuery q,
        [FromQuery] string? authorLogin = null,
        [FromQuery] string? gmLogin = null,
        [FromQuery] Guid? gameId = null)
    {
        var filter = new GameReviewFilter { GameId = gameId };

        if (!string.IsNullOrWhiteSpace(authorLogin))
        {
            var author = await _userReadingService.Get(authorLogin);
            filter.AuthorId = author.UserId;
        }

        if (!string.IsNullOrWhiteSpace(gmLogin))
        {
            var gm = await _userReadingService.Get(gmLogin);
            filter.GmId = gm.UserId;
        }

        var hasFilter = filter.AuthorId.HasValue || filter.GmId.HasValue || filter.GameId.HasValue;
        var (reviews, paging) = await _readingService.GetAllGameReviews(q, hasFilter ? filter : null);
        return Ok(new ListEnvelope<ApiReview>(reviews.Select(_mapper.Map<ApiReview>), new Paging(paging)));
    }

    /// <summary>
    /// Get all user reviews
    /// </summary>
    /// <remarks>
    /// Returns paginated list of all user reviews across all users.
    /// Supports filtering by author or recipient.
    /// </remarks>
    /// <param name="q">Paging parameters</param>
    /// <param name="authorLogin">Filter by review author login</param>
    /// <param name="recipientLogin">Filter by reviewed user login</param>
    /// <response code="200">List of user reviews</response>
    [HttpGet("users", Name = nameof(GetAllUserReviews))]
    [ProducesResponseType(typeof(ListEnvelope<ApiReview>), 200)]
    public async Task<IActionResult> GetAllUserReviews(
        [FromQuery] PagingQuery q,
        [FromQuery] string? authorLogin = null,
        [FromQuery] string? recipientLogin = null)
    {
        var filter = new UserReviewFilter();

        if (!string.IsNullOrWhiteSpace(authorLogin))
        {
            var author = await _userReadingService.Get(authorLogin);
            filter.AuthorId = author.UserId;
        }

        if (!string.IsNullOrWhiteSpace(recipientLogin))
        {
            var recipient = await _userReadingService.Get(recipientLogin);
            filter.RecipientId = recipient.UserId;
        }

        var hasFilter = filter.AuthorId.HasValue || filter.RecipientId.HasValue;
        var (reviews, paging) = await _readingService.GetAllUserReviews(q, hasFilter ? filter : null);
        return Ok(new ListEnvelope<ApiReview>(reviews.Select(_mapper.Map<ApiReview>), new Paging(paging)));
    }

    /// <summary>
    /// Get single review by ID
    /// </summary>
    /// <remarks>
    /// Returns any review (platform, game, or user) by its unique identifier.
    /// </remarks>
    /// <param name="id">Review ID</param>
    /// <response code="200">Review details</response>
    /// <response code="404">Review not found</response>
    [HttpGet("{id:guid}", Name = nameof(GetReview))]
    [ProducesResponseType(typeof(Envelope<ApiReview>), 200)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> GetReview(Guid id)
    {
        var review = await _readingService.Get(id);
        return Ok(new Envelope<ApiReview>(_mapper.Map<ApiReview>(review)));
    }

    /// <summary>
    /// Update review
    /// </summary>
    /// <remarks>
    /// Updates any review (platform, game, or user).
    /// Only the review author or moderators can update reviews.
    /// </remarks>
    /// <param name="id">Review ID</param>
    /// <param name="request">Updated review data</param>
    /// <response code="200">Review updated successfully</response>
    /// <response code="400">Invalid review data</response>
    /// <response code="401">User not authenticated</response>
    /// <response code="403">Not allowed to update this review</response>
    /// <response code="404">Review not found</response>
    [HttpPatch("{id:guid}", Name = nameof(UpdateReview))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<ApiReview>), 200)]
    [ProducesResponseType(typeof(BadRequestError), 400)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> UpdateReview(Guid id, [FromBody] UpdateReviewRequest request)
    {
        var updateReview = new UpdateReview
        {
            ReviewId = id,
            Text = request.Text,
            Approved = request.IsApproved
        };
        var review = await _updatingService.Update(updateReview);
        return Ok(new Envelope<ApiReview>(_mapper.Map<ApiReview>(review)));
    }

    /// <summary>
    /// Delete review
    /// </summary>
    /// <remarks>
    /// Deletes any review (platform, game, or user).
    /// Only the review author or moderators can delete reviews.
    /// </remarks>
    /// <param name="id">Review ID</param>
    /// <response code="204">Review deleted successfully</response>
    /// <response code="401">User not authenticated</response>
    /// <response code="403">Not allowed to delete this review</response>
    /// <response code="404">Review not found</response>
    [HttpDelete("{id:guid}", Name = nameof(DeleteReview))]
    [AuthenticationRequired]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> DeleteReview(Guid id)
    {
        await _deletingService.Delete(id);
        return NoContent();
    }
}
