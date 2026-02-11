using System;
using System.Threading.Tasks;
using DM.Services.Community.BusinessProcesses.Reviews.Reading;

namespace DM.Services.Community.BusinessProcesses.Reviews.Validation;

/// <summary>
/// Service for checking review eligibility rules
/// </summary>
public interface IReviewEligibilityService
{
    /// <summary>
    /// Check if two users have played together (were in the same game)
    /// </summary>
    /// <param name="userId1">First user ID</param>
    /// <param name="userId2">Second user ID</param>
    /// <returns>True if users have played together</returns>
    Task<bool> HavePlayedTogether(Guid userId1, Guid userId2);

    /// <summary>
    /// Check if user can leave a review for a game.
    /// User must have at least one post in the game.
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="gameId">Game ID</param>
    /// <returns>True if user has at least one post in the game</returns>
    Task<bool> CanReviewGame(Guid userId, Guid gameId);

    /// <summary>
    /// Check if user already has a review for the target user
    /// </summary>
    /// <param name="authorId">Review author ID</param>
    /// <param name="targetUserId">Target user ID</param>
    /// <returns>True if review already exists</returns>
    Task<bool> HasUserReview(Guid authorId, Guid targetUserId);

    /// <summary>
    /// Check if user already has a review for the game
    /// </summary>
    /// <param name="authorId">Review author ID</param>
    /// <param name="gameId">Game ID</param>
    /// <returns>True if review already exists</returns>
    Task<bool> HasGameReview(Guid authorId, Guid gameId);

    /// <summary>
    /// Check if user is a newbie (less than 100 game posts).
    /// Newbies cannot create User or Game reviews, and can only create neutral Post reviews.
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <returns>True if user has less than 100 posts</returns>
    Task<bool> IsNewbie(Guid userId);

    /// <summary>
    /// Check if review can be edited.
    /// Non-platform reviews can only be edited within 24 hours of creation.
    /// </summary>
    /// <param name="review">Review to check</param>
    /// <returns>True if review can still be edited</returns>
    bool CanEditReview(Review review);

    /// <summary>
    /// Check if user has created a post review in the specified game within the cooldown period (3 days).
    /// </summary>
    /// <param name="authorId">Review author ID</param>
    /// <param name="gameId">Game ID</param>
    /// <returns>True if user has a recent post review in this game</returns>
    Task<bool> HasRecentPostReviewInGame(Guid authorId, Guid gameId);
}
