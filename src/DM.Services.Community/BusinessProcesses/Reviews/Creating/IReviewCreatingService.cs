using System;
using System.Threading.Tasks;
using DM.Services.Community.BusinessProcesses.Reviews.Reading;
using DM.Services.Core.Dto.Enums;

namespace DM.Services.Community.BusinessProcesses.Reviews.Creating;

/// <summary>
/// Service for creating reviews (platform, user, game, post)
/// </summary>
public interface IReviewCreatingService
{
    /// <summary>
    /// Create new platform review
    /// </summary>
    /// <param name="createReview">Review data</param>
    /// <returns>Created review</returns>
    Task<Review> Create(CreateReview createReview);

    /// <summary>
    /// Create new user review
    /// </summary>
    /// <param name="targetUserId">Target user ID</param>
    /// <param name="text">Review text</param>
    /// <returns>Created review</returns>
    Task<Review> CreateUserReview(Guid targetUserId, string text);

    /// <summary>
    /// Create new game review
    /// </summary>
    /// <param name="gameId">Game ID</param>
    /// <param name="text">Review text</param>
    /// <returns>Created review</returns>
    Task<Review> CreateGameReview(Guid gameId, string text);

    /// <summary>
    /// Create new post review (rating)
    /// </summary>
    /// <param name="postId">Post ID</param>
    /// <param name="sign">Review sentiment sign</param>
    /// <param name="reasonType">Optional reason type</param>
    /// <returns>Created review</returns>
    Task<Review> CreatePostReview(Guid postId, ReviewSign sign, ReviewReasonType? reasonType = null);
}