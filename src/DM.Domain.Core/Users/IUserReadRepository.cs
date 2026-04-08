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
/// Implementation: Infrastructure.Services/Shared/Users/UserReadRepository
/// </remarks>
public interface IUserReadRepository
{
    // ═══ SINGLE USER ═══

    /// <summary>
    /// Get user by username
    /// </summary>
    Task<GeneralUser?> GetUserAsync(string username);

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
    /// Count users matching filter criteria
    /// </summary>
    Task<int> CountUsersAsync(
        UserActivityFilter filter,
        string? search = null,
        UserRole? role = null,
        bool? isHonorary = null,
        bool? isNewbie = null,
        bool? isOnline = null,
        int? minRating = null,
        int? maxRating = null,
        int? minGamesHosting = null,
        int? maxGamesHosting = null,
        int? minGamesPlaying = null,
        int? maxGamesPlaying = null,
        int? minBlogsHosting = null,
        int? maxBlogsHosting = null,
        DateTimeOffset? registeredFromUtc = null,
        DateTimeOffset? registeredToUtc = null);

    /// <summary>
    /// Get users list with pagination
    /// </summary>
    Task<IEnumerable<GeneralUser>> GetUsersAsync(
        PagingData paging,
        UserActivityFilter filter,
        string? search = null,
        UserRole? role = null,
        UserSort sort = UserSort.Name,
        bool sortAscending = true,
        bool? isHonorary = null,
        bool? isNewbie = null,
        bool? isOnline = null,
        int? minRating = null,
        int? maxRating = null,
        int? minGamesHosting = null,
        int? maxGamesHosting = null,
        int? minGamesPlaying = null,
        int? maxGamesPlaying = null,
        int? minBlogsHosting = null,
        int? maxBlogsHosting = null,
        DateTimeOffset? registeredFromUtc = null,
        DateTimeOffset? registeredToUtc = null);

    /// <summary>
    /// Get users by role
    /// </summary>
    Task<IEnumerable<GeneralUser>> GetUsersByRoleAsync(UserRole role);

    /// <summary>
    /// Get users by IDs
    /// </summary>
    Task<IEnumerable<GeneralUser>> GetUsersAsync(IEnumerable<Guid> userIds);
}
