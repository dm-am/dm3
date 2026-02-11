using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Services.Core.Dto;
using DM.Services.Core.Dto.Enums;

namespace DM.Services.Community.BusinessProcesses.Users.Reading;

/// <summary>
/// Community users storage
/// </summary>
internal interface IUserReadingRepository
{
    /// <summary>
    /// Count community users by filter
    /// </summary>
    /// <param name="filter">User activity filter</param>
    /// <param name="search">Search by login prefix</param>
    /// <returns>Number of the users</returns>
    Task<int> CountUsers(UserActivityFilter filter, string? search = null);

    /// <summary>
    /// Get users list on paging data
    /// </summary>
    /// <param name="paging">Paging data</param>
    /// <param name="filter">User activity filter</param>
    /// <param name="search">Search by login prefix</param>
    /// <returns>List of users found</returns>
    Task<IEnumerable<GeneralUser>> GetUsers(PagingData paging, UserActivityFilter filter, string? search = null);

    /// <summary>
    /// Get user by login
    /// </summary>
    /// <param name="login"></param>
    /// <returns></returns>
    Task<GeneralUser?> GetUser(string login);

    /// <summary>
    /// Get user by ID
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <returns>User found. Null if none found</returns>
    Task<GeneralUser?> GetUser(Guid userId);

    /// <summary>
    /// Get user details by login
    /// </summary>
    /// <param name="login">User login</param>
    /// <returns>User found. Null if none found</returns>
    Task<UserDetails?> GetUserDetails(string login);

    /// <summary>
    /// Get user details by ID
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <returns>User found. Null if none found</returns>
    Task<UserDetails?> GetUserDetails(Guid userId);

    /// <summary>
    /// Get user details by email
    /// </summary>
    /// <param name="email">User email</param>
    /// <returns>User found. Null if none found</returns>
    Task<UserDetails?> GetUserDetailsByEmail(string email);

    /// <summary>
    /// Get users by role
    /// </summary>
    /// <param name="role">User role</param>
    /// <returns>List of users with the specified role</returns>
    Task<IEnumerable<GeneralUser>> GetUsersByRole(UserRole role);

    /// <summary>
    /// Get count of post reviews given by user
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <returns>Number of post reviews given</returns>
    Task<int> GetPostReviewsGivenCount(Guid userId);
}