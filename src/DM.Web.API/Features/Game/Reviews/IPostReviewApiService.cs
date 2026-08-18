using System;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Game.Reviews;

/// <summary>
/// API service for post review operations
/// </summary>
public interface IPostReviewApiService
{
    /// <summary>
    /// Get post reviews across all games, optionally filtered
    /// </summary>
    /// <param name="query">Paging parameters</param>
    /// <param name="authorUsername">Filter by review author</param>
    /// <param name="recipientUsername">Filter by post author (recipient)</param>
    /// <param name="gameId">Filter by game</param>
    Task<ListEnvelope<PostReviewDto>> GetAll(
        PagingQuery query, string? authorUsername, string? recipientUsername, Guid? gameId);

    /// <summary>
    /// Get reviews for a specific post
    /// </summary>
    /// <param name="postId">Post identifier</param>
    /// <param name="query">Paging parameters</param>
    Task<ListEnvelope<PostReviewDto>> GetList(Guid postId, PagingQuery query);

    /// <summary>
    /// Get a single review of a named post
    /// </summary>
    /// <param name="postId">Post the review belongs to</param>
    /// <param name="reviewId">Review identifier</param>
    Task<Envelope<PostReviewDto>> Get(Guid postId, Guid reviewId);

    /// <summary>
    /// Create a review for a post
    /// </summary>
    /// <param name="postId">Post identifier</param>
    /// <param name="request">Review data</param>
    Task<Envelope<PostReviewDto>> Create(Guid postId, CreatePostReviewRequest request);

    /// <summary>
    /// Update a post review the caller authored
    /// </summary>
    /// <param name="reviewId">Review identifier</param>
    /// <param name="request">Fields to change</param>
    Task<Envelope<PostReviewDto>> Update(Guid reviewId, UpdatePostReviewRequest request);

    /// <summary>
    /// Delete a post review: its author, or a senior moderator
    /// </summary>
    /// <param name="reviewId">Review identifier</param>
    Task Delete(Guid reviewId);
}
