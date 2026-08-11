using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;

namespace DM.Domain.Core.Users;

/// <summary>
/// Read-only repository for user data. Used by Community and Moderation modules
/// for accessing user profile information without importing Domain.Personal.
/// </summary>
/// <remarks>
/// Full repository with write operations: Domain.Personal/Features/Profiles/IUserRepository
/// Implementation: Infrastructure.Persistence/Repositories/Personal/UserRepository,
/// registered as both IUserRepository and IUserReadRepository
/// </remarks>
public interface IUserReadRepository
{
    // ═══ SINGLE USER ═══

    /// <summary>
    /// Get user by username
    /// </summary>
    Task<GeneralUser?> GetUserAsync(string username);

    /// <summary>
    /// Identifier of a live user by username, or null when there is none.
    /// </summary>
    /// <remarks>
    /// A single scalar SELECT. <see cref="GetUserAsync(string)"/> hydrates the
    /// achievement counters, which costs twenty-one further queries, and every
    /// caller that only needed to answer "does this user exist, and what is
    /// their id" was paying that price.
    /// </remarks>
    Task<Guid?> FindUserIdAsync(string username);

    /// <summary>
    /// Get user by ID
    /// </summary>
    Task<GeneralUser?> GetUserAsync(Guid userId);

    /// <summary>
    /// Get user details by username
    /// </summary>
    Task<UserDetails?> GetUserDetailsAsync(string username);

    /// <summary>
    /// Get user details by ID
    /// </summary>
    Task<UserDetails?> GetUserDetailsAsync(Guid userId);

    // ═══ LIST USERS ═══

    /// <summary>
    /// Count users matching the filter. Takes the same filter object as
    /// <see cref="GetUsersAsync(PagingData, UserFilter)" /> so the total cannot
    /// describe a different set than the page does.
    /// </summary>
    Task<int> CountUsersAsync(UserFilter filter);

    /// <summary>
    /// Get users list with pagination
    /// </summary>
    Task<IEnumerable<GeneralUser>> GetUsersAsync(PagingData paging, UserFilter filter);

    /// <summary>
    /// Get users by role
    /// </summary>
    Task<IEnumerable<GeneralUser>> GetUsersByRoleAsync(UserRole role);

    /// <summary>
    /// Get users by IDs
    /// </summary>
    /// <remarks>
    /// Hydrates the achievement counters, which costs twenty-one further queries.
    /// A caller that only renders names and badges wants
    /// <see cref="GetUserReferencesAsync" /> instead.
    /// </remarks>
    Task<IEnumerable<GeneralUser>> GetUsersAsync(IEnumerable<Guid> userIds);

    /// <summary>
    /// Get the display-level essentials of users by IDs: one projection, no
    /// counter hydration.
    /// </summary>
    /// <remarks>
    /// A separate method rather than a flag on <see cref="GetUsersAsync(IEnumerable{Guid})" />
    /// so the return type cannot promise fields nobody filled: a
    /// <see cref="GeneralUser" /> with twenty-one zeroed counters is
    /// indistinguishable from a user who really has none.
    /// </remarks>
    Task<IEnumerable<UserReference>> GetUserReferencesAsync(IEnumerable<Guid> userIds);
}
