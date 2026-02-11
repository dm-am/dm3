using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Services.Core.Dto;

namespace DM.Services.Community.BusinessProcesses.Reviews.Reading;

/// <summary>
/// Service for reading reviews (platform, user, game)
/// </summary>
public interface IReviewReadingService
{
    /// <summary>
    /// Get platform reviews by query
    /// </summary>
    /// <param name="query">Paging query</param>
    /// <param name="onlyApproved">Only return approved reviews</param>
    /// <returns>Reviews and paging info</returns>
    Task<(IEnumerable<Review> reviews, PagingResult paging)> Get(PagingQuery query, bool onlyApproved);

    /// <summary>
    /// Get single review by ID
    /// </summary>
    /// <param name="id">Review ID</param>
    /// <returns>Review or null if not found</returns>
    Task<Review> Get(Guid id);

    /// <summary>
    /// Get reviews for a target user
    /// </summary>
    /// <param name="targetUserId">Target user ID</param>
    /// <param name="query">Paging query</param>
    /// <returns>Reviews and paging info</returns>
    Task<(IEnumerable<Review> reviews, PagingResult paging)> GetUserReviews(Guid targetUserId, PagingQuery query);

    /// <summary>
    /// Get reviews for a game
    /// </summary>
    /// <param name="gameId">Game ID</param>
    /// <param name="query">Paging query</param>
    /// <returns>Reviews and paging info</returns>
    Task<(IEnumerable<Review> reviews, PagingResult paging)> GetGameReviews(Guid gameId, PagingQuery query);

    /// <summary>
    /// Get user review by author (check if author already reviewed target user)
    /// </summary>
    /// <param name="targetUserId">Target user ID</param>
    /// <param name="authorId">Review author ID</param>
    /// <returns>Review or null if not found</returns>
    Task<Review?> GetUserReviewByAuthor(Guid targetUserId, Guid authorId);

    /// <summary>
    /// Get game review by author (check if author already reviewed game)
    /// </summary>
    /// <param name="gameId">Game ID</param>
    /// <param name="authorId">Review author ID</param>
    /// <returns>Review or null if not found</returns>
    Task<Review?> GetGameReviewByAuthor(Guid gameId, Guid authorId);

    /// <summary>
    /// Get all game reviews (all games) with optional filtering
    /// </summary>
    /// <param name="query">Paging query</param>
    /// <param name="filter">Optional filter parameters</param>
    /// <returns>Reviews and paging info</returns>
    Task<(IEnumerable<Review> reviews, PagingResult paging)> GetAllGameReviews(PagingQuery query, GameReviewFilter? filter = null);

    /// <summary>
    /// Get all user reviews (all users) with optional filtering
    /// </summary>
    /// <param name="query">Paging query</param>
    /// <param name="filter">Optional filter parameters</param>
    /// <returns>Reviews and paging info</returns>
    Task<(IEnumerable<Review> reviews, PagingResult paging)> GetAllUserReviews(PagingQuery query, UserReviewFilter? filter = null);

    /// <summary>
    /// Get reviews for a specific post
    /// </summary>
    /// <param name="postId">Post ID</param>
    /// <param name="query">Paging query</param>
    /// <returns>Reviews and paging info</returns>
    Task<(IEnumerable<Review> reviews, PagingResult paging)> GetPostReviews(Guid postId, PagingQuery query);

    /// <summary>
    /// Get post review by author (check if author already reviewed post)
    /// </summary>
    /// <param name="postId">Post ID</param>
    /// <param name="authorId">Review author ID</param>
    /// <returns>Review or null if not found</returns>
    Task<Review?> GetPostReviewByAuthor(Guid postId, Guid authorId);

    /// <summary>
    /// Get all post reviews with optional filtering
    /// </summary>
    /// <param name="query">Paging query</param>
    /// <param name="filter">Optional filter parameters</param>
    /// <returns>Reviews and paging info</returns>
    Task<(IEnumerable<Review> reviews, PagingResult paging)> GetAllPostReviews(PagingQuery query, PostReviewFilter? filter = null);
}