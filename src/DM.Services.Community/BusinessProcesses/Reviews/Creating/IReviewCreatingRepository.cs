using System;
using System.Threading.Tasks;
using DM.Services.Community.BusinessProcesses.Reviews.Reading;
using DbReview = DM.Services.DataAccess.BusinessObjects.Common.Review;

namespace DM.Services.Community.BusinessProcesses.Reviews.Creating;

/// <summary>
/// Storage for review creating
/// </summary>
internal interface IReviewCreatingRepository
{
    /// <summary>
    /// Check if user already has a review
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <returns>True if user already has a review</returns>
    Task<bool> UserHasReview(Guid userId);

    /// <summary>
    /// Create new review
    /// </summary>
    /// <param name="review"></param>
    /// <returns></returns>
    Task<Review> Create(DbReview review);

    /// <summary>
    /// Get post information for review creation (author ID, game ID)
    /// </summary>
    /// <param name="postId">Post identifier</param>
    /// <returns>Post info or null if not found</returns>
    Task<PostInfo?> GetPostInfo(Guid postId);

    /// <summary>
    /// Get post review by author
    /// </summary>
    /// <param name="postId">Post identifier</param>
    /// <param name="authorId">Review author identifier</param>
    /// <returns>Review or null if not found</returns>
    Task<Review?> GetPostReviewByAuthor(Guid postId, Guid authorId);
}

/// <summary>
/// Post information for review creation
/// </summary>
public class PostInfo
{
    /// <summary>
    /// Post author identifier
    /// </summary>
    public Guid AuthorId { get; set; }

    /// <summary>
    /// Game identifier
    /// </summary>
    public Guid GameId { get; set; }
}