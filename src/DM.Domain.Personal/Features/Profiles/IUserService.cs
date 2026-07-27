using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Users;

namespace DM.Domain.Personal.Features.Profiles;

/// <summary>
/// Service for user operations.
/// Extends IUserLookupService with full user management capabilities.
/// </summary>
public interface IUserService : IUserLookupService
{
    // NOTE: GetAsync(string username) and GetAsync(Guid userId) are inherited from IUserLookupService

    /// <summary>
    /// Get community users list (paginated)
    /// </summary>
    Task<(IEnumerable<GeneralUser> users, PagingResult paging)> GetAsync(
        PagingQuery query,
        UserActivityFilter filter,
        string? search = null,
        UserRole? role = null,
        UserSort sort = UserSort.Name);

    /// <summary>
    /// Get current authenticated user
    /// </summary>
    Task<GeneralUser> GetCurrentAsync();

    /// <summary>
    /// Get user details by username
    /// </summary>
    Task<Core.Users.UserDetails> GetDetailsAsync(string username);

    /// <summary>
    /// Get user details by ID
    /// </summary>
    Task<Core.Users.UserDetails> GetDetailsAsync(Guid userId);

    /// <summary>
    /// Get users by role
    /// </summary>
    Task<IEnumerable<GeneralUser>> GetByRoleAsync(UserRole role);

    /// <summary>
    /// Get username history for a user
    /// </summary>
    Task<IReadOnlyCollection<UsernameHistoryEntry>> GetUsernameHistoryAsync(Guid userId);

    /// <summary>
    /// Update user details
    /// </summary>
    Task<Core.Users.UserDetails> UpdateAsync(UpdateUser updateUser);

    /// <summary>
    /// Reset the user's avatar. Idempotent (no avatar — no-op).
    /// Triggers GC of the old Upload (S3 + DB cleanup via the background worker).
    /// </summary>
    Task RemoveAvatarAsync(Guid userId);
}
