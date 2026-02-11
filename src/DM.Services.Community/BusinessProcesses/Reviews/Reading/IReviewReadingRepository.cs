using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Services.Core.Dto;

namespace DM.Services.Community.BusinessProcesses.Reviews.Reading;

/// <summary>
/// Storage for reading reviews (platform, user, game)
/// </summary>
internal interface IReviewReadingRepository
{
    /// <summary>
    /// Get total count of platform reviews
    /// </summary>
    /// <param name="approvedOnly">Only count approved reviews</param>
    /// <returns>Count</returns>
    Task<int> Count(bool approvedOnly);

    /// <summary>
    /// Get platform reviews list
    /// </summary>
    /// <param name="paging">Paging data</param>
    /// <param name="approvedOnly">Only return approved reviews</param>
    /// <returns>Reviews</returns>
    Task<IEnumerable<Review>> Get(PagingData paging, bool approvedOnly);

    /// <summary>
    /// Get single review by ID
    /// </summary>
    /// <param name="id">Review identifier</param>
    /// <returns>Review or null</returns>
    Task<Review?> Get(Guid id);

    /// <summary>
    /// Get count of user reviews for a target user
    /// </summary>
    /// <param name="targetUserId">Target user ID</param>
    /// <returns>Count</returns>
    Task<int> CountUserReviews(Guid targetUserId);

    /// <summary>
    /// Get user reviews for a target user
    /// </summary>
    /// <param name="targetUserId">Target user ID</param>
    /// <param name="paging">Paging data</param>
    /// <returns>Reviews</returns>
    Task<IEnumerable<Review>> GetUserReviews(Guid targetUserId, PagingData paging);

    /// <summary>
    /// Get count of game reviews
    /// </summary>
    /// <param name="gameId">Game ID</param>
    /// <returns>Count</returns>
    Task<int> CountGameReviews(Guid gameId);

    /// <summary>
    /// Get game reviews
    /// </summary>
    /// <param name="gameId">Game ID</param>
    /// <param name="paging">Paging data</param>
    /// <returns>Reviews</returns>
    Task<IEnumerable<Review>> GetGameReviews(Guid gameId, PagingData paging);

    /// <summary>
    /// Get user review by author
    /// </summary>
    /// <param name="targetUserId">Target user ID</param>
    /// <param name="authorId">Author ID</param>
    /// <returns>Review or null</returns>
    Task<Review?> GetUserReviewByAuthor(Guid targetUserId, Guid authorId);

    /// <summary>
    /// Get game review by author
    /// </summary>
    /// <param name="gameId">Game ID</param>
    /// <param name="authorId">Author ID</param>
    /// <returns>Review or null</returns>
    Task<Review?> GetGameReviewByAuthor(Guid gameId, Guid authorId);

    /// <summary>
    /// Get count of all game reviews with optional filter
    /// </summary>
    /// <param name="filter">Optional filter</param>
    /// <returns>Count</returns>
    Task<int> CountAllGameReviews(GameReviewFilter? filter = null);

    /// <summary>
    /// Get all game reviews with optional filter
    /// </summary>
    /// <param name="paging">Paging data</param>
    /// <param name="filter">Optional filter</param>
    /// <returns>Reviews</returns>
    Task<IEnumerable<Review>> GetAllGameReviews(PagingData paging, GameReviewFilter? filter = null);

    /// <summary>
    /// Get count of all user reviews with optional filter
    /// </summary>
    /// <param name="filter">Optional filter</param>
    /// <returns>Count</returns>
    Task<int> CountAllUserReviews(UserReviewFilter? filter = null);

    /// <summary>
    /// Get all user reviews with optional filter
    /// </summary>
    /// <param name="paging">Paging data</param>
    /// <param name="filter">Optional filter</param>
    /// <returns>Reviews</returns>
    Task<IEnumerable<Review>> GetAllUserReviews(PagingData paging, UserReviewFilter? filter = null);

    /// <summary>
    /// Get count of post reviews for a specific post
    /// </summary>
    /// <param name="postId">Post ID</param>
    /// <returns>Count</returns>
    Task<int> CountPostReviews(Guid postId);

    /// <summary>
    /// Get post reviews for a specific post
    /// </summary>
    /// <param name="postId">Post ID</param>
    /// <param name="paging">Paging data</param>
    /// <returns>Reviews</returns>
    Task<IEnumerable<Review>> GetPostReviews(Guid postId, PagingData paging);

    /// <summary>
    /// Get post review by author
    /// </summary>
    /// <param name="postId">Post ID</param>
    /// <param name="authorId">Author ID</param>
    /// <returns>Review or null</returns>
    Task<Review?> GetPostReviewByAuthor(Guid postId, Guid authorId);

    /// <summary>
    /// Get count of all post reviews with optional filter
    /// </summary>
    /// <param name="filter">Optional filter</param>
    /// <returns>Count</returns>
    Task<int> CountAllPostReviews(PostReviewFilter? filter = null);

    /// <summary>
    /// Get all post reviews with optional filter
    /// </summary>
    /// <param name="paging">Paging data</param>
    /// <param name="filter">Optional filter</param>
    /// <returns>Reviews</returns>
    Task<IEnumerable<Review>> GetAllPostReviews(PagingData paging, PostReviewFilter? filter = null);
}