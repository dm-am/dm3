using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;

namespace DM.Web.API.Features.Blog.Users;

/// <summary>
/// API service for blog users
/// </summary>
public interface IBlogUserApiService
{
    #region Users

    /// <summary>
    /// Get all users of a blog, optionally filtered by role
    /// </summary>
    /// <param name="blogId">Blog ID</param>
    /// <param name="role">Optional role filter (owner, assistant, mentor, reader)</param>
    Task<IEnumerable<BlogUser>> GetUsers(Guid blogId, BlogRole? role = null);

    /// <summary>
    /// Remove user from blog by user ID
    /// </summary>
    Task RemoveUser(Guid blogId, Guid userId);

    #endregion

    #region Readers

    /// <summary>
    /// Get list of blog readers (subscribers)
    /// </summary>
    Task<IEnumerable<BlogUser>> GetReaders(Guid blogId);

    /// <summary>
    /// Subscribe to blog as reader
    /// </summary>
    Task<BlogUser> Subscribe(Guid blogId);

    /// <summary>
    /// Unsubscribe from blog
    /// </summary>
    Task Unsubscribe(Guid blogId);

    // Note: RemoveReader is not provided - readers can only unsubscribe themselves
    // Use blacklist to prevent problematic users from commenting

    #endregion

    #region Assistants

    /// <summary>
    /// Get list of blog assistants
    /// </summary>
    Task<IEnumerable<BlogUser>> GetAssistants(Guid blogId);

    /// <summary>
    /// Remove assistant from blog by username
    /// </summary>
    Task RemoveAssistantByUsername(Guid blogId, string username);

    #endregion
}
