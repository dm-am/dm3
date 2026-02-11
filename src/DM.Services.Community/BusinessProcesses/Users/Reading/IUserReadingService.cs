using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Services.Core.Dto;
using DM.Services.Core.Dto.Enums;

namespace DM.Services.Community.BusinessProcesses.Users.Reading;

/// <summary>
/// Community-related service
/// </summary>
public interface IUserReadingService
{
    /// <summary>
    /// Get community users list
    /// </summary>
    /// <param name="query">Paging query</param>
    /// <param name="filter">User activity filter</param>
    /// <param name="search">Search by login prefix</param>
    /// <returns>Pair of found users and paging data</returns>
    Task<(IEnumerable<GeneralUser> users, PagingResult paging)> Get(PagingQuery query, UserActivityFilter filter, string? search = null);

    /// <summary>
    /// Get community user short info by login
    /// </summary>
    /// <param name="login">User login</param>
    /// <returns></returns>
    Task<GeneralUser> Get(string login);

    /// <summary>
    /// Get community user short info by ID
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <returns>Found user</returns>
    Task<GeneralUser> Get(Guid userId);

    /// <summary>
    /// Get current authenticated user
    /// </summary>
    /// <returns>Current user</returns>
    Task<GeneralUser> GetCurrent();

    /// <summary>
    /// Get community user details by login
    /// </summary>
    /// <param name="login">User login</param>
    /// <returns>Found user</returns>
    Task<UserDetails> GetDetails(string login);

    /// <summary>
    /// Get community user details by ID
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <returns>Found user</returns>
    Task<UserDetails> GetDetails(Guid userId);

    /// <summary>
    /// Get users by role
    /// </summary>
    /// <param name="role">User role</param>
    /// <returns>List of users with the specified role</returns>
    Task<IEnumerable<GeneralUser>> GetByRole(UserRole role);
}