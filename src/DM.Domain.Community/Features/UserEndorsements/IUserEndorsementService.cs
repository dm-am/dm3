using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;

namespace DM.Domain.Community.Features.UserEndorsements;

/// <summary>
/// Service for user endorsement operations
/// </summary>
public interface IUserEndorsementService
{
    /// <summary>
    /// Create new user endorsement
    /// </summary>
    /// <param name="createEndorsement">Endorsement data</param>
    /// <returns>Created endorsement</returns>
    Task<UserEndorsement> CreateAsync(CreateUserEndorsement createEndorsement);

    /// <summary>
    /// Tell whether the current user may write a recommendation about the
    /// given user, and why not when they may not
    /// </summary>
    /// <remarks>
    /// Answered by the same evaluation <see cref="CreateAsync"/> refuses by,
    /// so a client that hides the control on a refusal here hides exactly the
    /// control the create call would have rejected.
    /// </remarks>
    /// <param name="targetUserId">Prospective recipient</param>
    Task<UserEndorsementEligibility> GetEligibilityAsync(Guid targetUserId);

    /// <summary>
    /// Get single endorsement by ID
    /// </summary>
    /// <param name="id">Endorsement ID</param>
    /// <returns>Endorsement or throws if not found</returns>
    Task<UserEndorsement> GetAsync(Guid id);

    /// <summary>
    /// Get endorsements for a target user
    /// </summary>
    /// <param name="targetUserId">Target user ID</param>
    /// <param name="query">Paging query</param>
    /// <returns>Endorsements and paging info</returns>
    Task<(IEnumerable<UserEndorsement> Endorsements, PagingResult Paging)> GetListAsync(Guid targetUserId, PagingQuery query);

    /// <summary>
    /// Get all user endorsements with optional filtering
    /// </summary>
    /// <param name="query">Paging query</param>
    /// <param name="filter">Optional filter parameters</param>
    /// <returns>Endorsements and paging info</returns>
    Task<(IEnumerable<UserEndorsement> Endorsements, PagingResult Paging)> GetAllAsync(PagingQuery query, UserEndorsementFilter? filter = null);

    /// <summary>
    /// Get user endorsement by author (check if author already endorsed target user)
    /// </summary>
    /// <param name="targetUserId">Target user ID</param>
    /// <param name="authorId">Endorsement author ID</param>
    /// <returns>Endorsement or null if not found</returns>
    Task<UserEndorsement?> GetByAuthorAsync(Guid targetUserId, Guid authorId);

    /// <summary>
    /// Update user endorsement
    /// </summary>
    /// <param name="updateEndorsement">Update data</param>
    /// <returns>Updated endorsement</returns>
    Task<UserEndorsement> UpdateAsync(UpdateUserEndorsement updateEndorsement);

    /// <summary>
    /// Delete user endorsement
    /// </summary>
    /// <param name="id">Endorsement ID</param>
    Task DeleteAsync(Guid id);

    /// <summary>
    /// Check if user already has an endorsement for the target user
    /// </summary>
    /// <param name="authorId">Endorsement author ID</param>
    /// <param name="targetUserId">Target user ID</param>
    /// <returns>True if endorsement already exists</returns>
    Task<bool> ExistsAsync(Guid authorId, Guid targetUserId);

    /// <summary>
    /// Check if two users have played together (were in the same game)
    /// </summary>
    /// <param name="userId1">First user ID</param>
    /// <param name="userId2">Second user ID</param>
    /// <returns>True if users have played together</returns>
    Task<bool> HavePlayedTogetherAsync(Guid userId1, Guid userId2);

    /// <summary>
    /// Check if endorsement can be edited (within 24 hours of creation)
    /// </summary>
    /// <param name="endorsement">Endorsement to check</param>
    /// <returns>True if endorsement can still be edited</returns>
    bool CanEdit(UserEndorsement endorsement);
}
