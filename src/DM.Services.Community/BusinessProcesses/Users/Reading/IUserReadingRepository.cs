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
    /// <param name="withInactive">Count inactive users too</param>
    /// <param name="search">Search by login prefix</param>
    /// <returns>Number of the users</returns>
    Task<int> CountUsers(bool withInactive, string search = null);

    /// <summary>
    /// Get users list on paging data
    /// </summary>
    /// <param name="paging">Paging data</param>
    /// <param name="withInactive">Search among inactive users</param>
    /// <param name="search">Search by login prefix</param>
    /// <returns>List of users found</returns>
    Task<IEnumerable<GeneralUser>> GetUsers(PagingData paging, bool withInactive, string search = null);

    /// <summary>
    /// Get user by login
    /// </summary>
    /// <param name="login"></param>
    /// <returns></returns>
    Task<GeneralUser> GetUser(string login);

    /// <summary>
    /// Get user by ID
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <returns>User found. Null if none found</returns>
    Task<GeneralUser> GetUser(Guid userId);

    /// <summary>
    /// Get user details by login
    /// </summary>
    /// <param name="login">User login</param>
    /// <returns>User found. Null if none found</returns>
    Task<UserDetails> GetUserDetails(string login);

    /// <summary>
    /// Get user details by ID
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <returns>User found. Null if none found</returns>
    Task<UserDetails> GetUserDetails(Guid userId);

    /// <summary>
    /// Get users by role
    /// </summary>
    /// <param name="role">User role</param>
    /// <returns>List of users with the specified role</returns>
    Task<IEnumerable<GeneralUser>> GetUsersByRole(UserRole role);
}