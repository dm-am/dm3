using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Reviews;

namespace DM.Domain.Community.Features.PlatformReviews;

/// <summary>
/// Repository for platform review operations
/// </summary>
public interface IPlatformReviewRepository
{
    /// <summary>
    /// Get total count of platform reviews
    /// </summary>
    /// <param name="approvedOnly">Whether to count only approved reviews</param>
    Task<int> CountAsync(bool approvedOnly);

    /// <summary>
    /// Get platform reviews list
    /// </summary>
    /// <param name="paging">Paging parameters</param>
    /// <param name="approvedOnly">Whether to return only approved reviews</param>
    Task<IEnumerable<Review>> GetAsync(PagingData paging, bool approvedOnly);

    /// <summary>
    /// Get single platform review by ID
    /// </summary>
    /// <param name="id">Review ID</param>
    Task<Review?> GetAsync(Guid id);

    /// <summary>
    /// Check if user already has a platform review
    /// </summary>
    /// <param name="userId">User ID</param>
    Task<bool> UserHasReviewAsync(Guid userId);

    /// <summary>
    /// Create new platform review
    /// </summary>
    /// <param name="entity">Review data</param>
    Task<Review> CreateAsync(CreatePlatformReviewEntity entity);

    /// <summary>
    /// Update platform review
    /// </summary>
    /// <param name="entity">Update data</param>
    Task<Review> UpdateAsync(UpdatePlatformReviewEntity entity);
}
