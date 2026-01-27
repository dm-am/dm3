using System;
using System.Threading.Tasks;
using DM.Services.Core.Dto.Enums;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Dto.Users;
using Microsoft.AspNetCore.Http;

namespace DM.Web.API.Services.Users;

/// <summary>
/// API service for community users
/// </summary>
public interface IUserApiService
{
    /// <summary>
    /// Get community users
    /// </summary>
    /// <param name="query">Paging query</param>
    /// <returns></returns>
    Task<ListEnvelope<User>> GetUsers(UsersQuery query);

    /// <summary>
    /// Get community user by login
    /// </summary>
    /// <param name="login">User login</param>
    /// <returns></returns>
    Task<Envelope<User>> GetUser(string login);

    /// <summary>
    /// Get community user by ID
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <returns></returns>
    Task<Envelope<User>> GetUser(Guid userId);

    /// <summary>
    /// Get current authenticated user
    /// </summary>
    /// <returns></returns>
    Task<Envelope<User>> GetCurrentUser();

    /// <summary>
    /// Get community user details by login
    /// </summary>
    /// <param name="login"></param>
    /// <returns></returns>
    Task<Envelope<UserDetails>> GetUserDetails(string login);

    /// <summary>
    /// Get community user details by ID
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <returns></returns>
    Task<Envelope<UserDetails>> GetUserDetails(Guid userId);

    /// <summary>
    /// Update user
    /// </summary>
    /// <param name="login">User login</param>
    /// <param name="user">User information</param>
    /// <returns></returns>
    Task<Envelope<UserDetails>> UpdateUser(string login, UserDetails user);

    /// <summary>
    /// Upload user profile picture
    /// </summary>
    /// <param name="login">User login</param>
    /// <param name="files">Profile picture files</param>
    /// <returns></returns>
    Task<Envelope<UserDetails>> UploadProfilePicture(string login, IFormFile files);

    /// <summary>
    /// Get users by role
    /// </summary>
    /// <param name="role">User role</param>
    /// <returns></returns>
    Task<ListEnvelope<User>> GetUsersByRole(UserRole role);

    /// <summary>
    /// Get user settings
    /// </summary>
    /// <param name="login">User login</param>
    /// <returns></returns>
    Task<Envelope<UserSettings>> GetUserSettings(string login);

    /// <summary>
    /// Update user settings
    /// </summary>
    /// <param name="login">User login</param>
    /// <param name="settings">Settings to update</param>
    /// <returns></returns>
    Task<Envelope<UserSettings>> UpdateUserSettings(string login, UserSettings settings);
}