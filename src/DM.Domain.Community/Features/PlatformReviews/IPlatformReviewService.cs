using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Reviews;

namespace DM.Domain.Community.Features.PlatformReviews;

/// <summary>
/// Service for platform/site review operations
/// </summary>
public interface IPlatformReviewService
{
    /// <summary>
    /// Create new platform review
    /// </summary>
    /// <param name="createReview">Review data</param>
    /// <returns>Created review</returns>
    Task<Review> CreateAsync(CreatePlatformReview createReview);

    /// <summary>
    /// Get single review by ID
    /// </summary>
    /// <param name="id">Review ID</param>
    /// <returns>Review or throws if not found</returns>
    Task<Review> GetAsync(Guid id);

    /// <summary>
    /// Get platform reviews with paging
    /// </summary>
    /// <param name="query">Paging query</param>
    /// <param name="onlyApproved">Only return approved reviews</param>
    /// <returns>Reviews and paging info</returns>
    Task<(IEnumerable<Review> Reviews, PagingResult Paging)> GetListAsync(PagingQuery query, bool onlyApproved);

    /// <summary>
    /// Update platform review (text and/or approval status)
    /// </summary>
    /// <param name="updateReview">Update data</param>
    /// <returns>Updated review</returns>
    Task<Review> UpdateAsync(UpdatePlatformReview updateReview);

    /// <summary>
    /// Delete platform review
    /// </summary>
    /// <param name="id">Review ID</param>
    Task DeleteAsync(Guid id);

    /// <summary>
    /// Approve or reject platform review
    /// </summary>
    /// <param name="id">Review ID</param>
    /// <param name="approved">Approval status</param>
    /// <returns>Updated review</returns>
    Task<Review> SetApprovalAsync(Guid id, bool approved);
}
