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
}