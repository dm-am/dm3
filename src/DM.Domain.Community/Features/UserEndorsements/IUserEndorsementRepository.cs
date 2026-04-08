using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;

namespace DM.Domain.Community.Features.UserEndorsements;

/// <summary>
/// Repository for user endorsement operations
/// </summary>
public interface IUserEndorsementRepository
{
    // ═══ READ ═══

    /// <summary>
    /// Get total count of endorsements for a target user
    /// </summary>
    /// <param name="targetUserId">Target user ID</param>
    Task<int> CountAsync(Guid targetUserId);

    /// <summary>
    /// Get endorsements for a target user
    /// </summary>
    /// <param name="targetUserId">Target user ID</param>
    /// <param name="paging">Paging parameters</param>
    Task<IEnumerable<UserEndorsement>> GetAsync(Guid targetUserId, PagingData paging);

    /// <summary>
    /// Get single user endorsement by ID
    /// </summary>
    /// <param name="id">Endorsement ID</param>
    Task<UserEndorsement?> GetAsync(Guid id);

    /// <summary>
    /// Get user endorsement by author
    /// </summary>
    /// <param name="targetUserId">Target user ID</param>
    /// <param name="authorId">Author ID</param>
    Task<UserEndorsement?> GetByAuthorAsync(Guid targetUserId, Guid authorId);

    /// <summary>
    /// Get count of all user endorsements with optional filter
    /// </summary>
    /// <param name="filter">Optional filter parameters</param>
    Task<int> CountAllAsync(UserEndorsementFilter? filter = null);

    /// <summary>
    /// Get all user endorsements with optional filter
    /// </summary>
    /// <param name="paging">Paging parameters</param>
    /// <param name="filter">Optional filter parameters</param>
    Task<IEnumerable<UserEndorsement>> GetAllAsync(PagingData paging, UserEndorsementFilter? filter = null);

    // ═══ WRITE ═══

    /// <summary>
    /// Check if user already has an endorsement for target user
    /// </summary>
    /// <param name="authorId">Author ID</param>
    /// <param name="targetUserId">Target user ID</param>
    Task<bool> ExistsAsync(Guid authorId, Guid targetUserId);

    /// <summary>
    /// Create new user endorsement
    /// </summary>
    /// <param name="entity">Endorsement data</param>
    Task<UserEndorsement> CreateAsync(CreateUserEndorsementEntity entity);

    /// <summary>
    /// Update user endorsement
    /// </summary>
    /// <param name="entity">Update data</param>
    Task<UserEndorsement> UpdateAsync(UpdateUserEndorsementEntity entity);

    // ═══ ELIGIBILITY ═══

    /// <summary>
    /// Check if two users have played together in the same game
    /// </summary>
    /// <param name="userId1">First user ID</param>
    /// <param name="userId2">Second user ID</param>
    Task<bool> HavePlayedTogetherAsync(Guid userId1, Guid userId2);

    /// <summary>
    /// Get user's total post count in games
    /// </summary>
    /// <param name="userId">User ID</param>
    Task<int> GetUserPostCountAsync(Guid userId);
}
