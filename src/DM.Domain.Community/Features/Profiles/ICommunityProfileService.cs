using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Users;

namespace DM.Domain.Community.Features.Profiles;

/// <summary>
/// Service for public user profiles (Community module).
/// Provides read-only access to user information for public display.
/// </summary>
public interface ICommunityProfileService
{
    /// <summary>
    /// Get public user profile by username
    /// </summary>
    Task<UserDetails> GetProfile(string username);

    /// <summary>
    /// Get public user profile by user ID
    /// </summary>
    Task<UserDetails> GetProfile(Guid userId);

    /// <summary>
    /// Get community users list (paginated)
    /// </summary>
    Task<(IEnumerable<GeneralUser> users, PagingResult paging)> GetUsers(
        PagingQuery query,
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
    /// Get users by role (e.g., moderators, administrators)
    /// </summary>
    Task<IEnumerable<GeneralUser>> GetUsersByRole(UserRole role);

    /// <summary>
    /// Get username history for a user
    /// </summary>
    Task<IReadOnlyCollection<UsernameHistoryEntry>> GetUsernameHistory(Guid userId);
}
