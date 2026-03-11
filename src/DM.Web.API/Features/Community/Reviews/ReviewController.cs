using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Community.Features.PlatformReviews;
using DM.Domain.Community.Features.UserReviews;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Game.Features.Games;
using DM.Domain.Core.Reviews;
using DM.Domain.Core.Users;
using DM.Domain.Game.Features.Reviews;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Features.Community.Reviews;

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
    private readonly IPlatformReviewService _platformReviewService;
    private readonly IUserReviewService _userReviewService;
    private readonly IGameReviewService _gameReviewService;
    private readonly IPostReviewService _postReviewService;
    private readonly IUserLookupService _userLookupService;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public ReviewController(
        IPlatformReviewService platformReviewService,
        IUserReviewService userReviewService,
        IGameReviewService gameReviewService,
        IPostReviewService postReviewService,
        IUserLookupService userLookupService,
        IMapper mapper)
    {
        _platformReviewService = platformReviewService;
        _userReviewService = userReviewService;
        _gameReviewService = gameReviewService;
        _postReviewService = postReviewService;
        _userLookupService = userLookupService;
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
    [ProducesResponseType(typeof(ListEnvelope<Review>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllGameReviews(
        [FromQuery] PagingQuery q,
        [FromQuery] string? authorLogin = null,
        [FromQuery] string? gmLogin = null,
        [FromQuery] Guid? gameId = null)
    {
        var filter = new GameReviewFilter { GameId = gameId };

        if (!string.IsNullOrWhiteSpace(authorLogin))
        {
            var author = await _userLookupService.GetAsync(authorLogin);
            filter.AuthorId = author.UserId;
        }

        if (!string.IsNullOrWhiteSpace(gmLogin))
        {
            var gm = await _userLookupService.GetAsync(gmLogin);
            filter.GmId = gm.UserId;
        }

        var hasFilter = filter.AuthorId.HasValue || filter.GmId.HasValue || filter.GameId.HasValue;
        var (reviews, paging) = await _gameReviewService.GetAllAsync(q, hasFilter ? filter : null);
        return Ok(new ListEnvelope<Review>(reviews.Select(_mapper.Map<Review>), new PagingInfo(paging)));
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
    [ProducesResponseType(typeof(ListEnvelope<Review>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllUserReviews(
        [FromQuery] PagingQuery q,
        [FromQuery] string? authorLogin = null,
        [FromQuery] string? recipientLogin = null)
    {
        var filter = new UserReviewFilter();

        if (!string.IsNullOrWhiteSpace(authorLogin))
        {
            var author = await _userLookupService.GetAsync(authorLogin);
            filter.AuthorId = author.UserId;
        }

        if (!string.IsNullOrWhiteSpace(recipientLogin))
        {
            var recipient = await _userLookupService.GetAsync(recipientLogin);
            filter.RecipientId = recipient.UserId;
        }

        var hasFilter = filter.AuthorId.HasValue || filter.RecipientId.HasValue;
        var (reviews, paging) = await _userReviewService.GetAllAsync(q, hasFilter ? filter : null);
        return Ok(new ListEnvelope<Review>(reviews.Select(_mapper.Map<Review>), new PagingInfo(paging)));
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
    [ProducesResponseType(typeof(Envelope<Review>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetReview(Guid id)
    {
        var review = await FindReviewAsync(id);
        return Ok(new Envelope<Review>(_mapper.Map<Review>(review)));
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
    [ProducesResponseType(typeof(Envelope<Review>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BadRequestError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateReview(Guid id, [FromBody] UpdateReviewRequest request)
    {
        var existingReview = await FindReviewAsync(id);

        DM.Domain.Core.Reviews.Review updatedReview = existingReview.TargetType switch
        {
            ReviewTargetType.Platform => await _platformReviewService.UpdateAsync(new UpdatePlatformReview
            {
                ReviewId = id,
                Text = request.Text,
                Approved = request.IsApproved
            }),
            ReviewTargetType.User => await _userReviewService.UpdateAsync(new UpdateUserReview
            {
                ReviewId = id,
                Text = request.Text
            }),
            ReviewTargetType.Game => await _gameReviewService.UpdateAsync(new UpdateGameReview
            {
                ReviewId = id,
                Text = request.Text
            }),
            ReviewTargetType.Post => await _postReviewService.UpdateAsync(new UpdatePostReview
            {
                ReviewId = id,
                Sign = request.Sign,
                ReasonType = request.ReasonType
            }),
            _ => throw new HttpException(HttpStatusCode.BadRequest, "Unknown review type")
        };

        return Ok(new Envelope<Review>(_mapper.Map<Review>(updatedReview)));
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
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteReview(Guid id)
    {
        var existingReview = await FindReviewAsync(id);

        switch (existingReview.TargetType)
        {
            case ReviewTargetType.Platform:
                await _platformReviewService.DeleteAsync(id);
                break;
            case ReviewTargetType.User:
                await _userReviewService.DeleteAsync(id);
                break;
            case ReviewTargetType.Game:
                await _gameReviewService.DeleteAsync(id);
                break;
            case ReviewTargetType.Post:
                await _postReviewService.DeleteAsync(id);
                break;
            default:
                throw new HttpException(HttpStatusCode.BadRequest, "Unknown review type");
        }

        return NoContent();
    }

    private async Task<DM.Domain.Core.Reviews.Review> FindReviewAsync(Guid id)
    {
        // Try each service in order until we find the review
        // Platform reviews
        try
        {
            return await _platformReviewService.GetAsync(id);
        }
        catch (HttpException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // Not a platform review, continue
        }

        // User reviews
        try
        {
            return await _userReviewService.GetAsync(id);
        }
        catch (HttpException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // Not a user review, continue
        }

        // Game reviews
        try
        {
            return await _gameReviewService.GetAsync(id);
        }
        catch (HttpException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // Not a game review, continue
        }

        // Post reviews
        try
        {
            return await _postReviewService.GetAsync(id);
        }
        catch (HttpException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // Not found in any service
        }

        throw new HttpException(HttpStatusCode.NotFound, "Review not found");
    }
}
