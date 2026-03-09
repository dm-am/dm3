using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Web.API.Features.Community.Users;

namespace DM.Web.API.Features.Blog.Blacklists;

/// <summary>
/// API service for blog blacklist
/// </summary>
public interface IBlogBlacklistApiService
{
    /// <summary>
    /// Get list of blacklisted users for the blog
    /// </summary>
    Task<IEnumerable<User>> Get(Guid blogId);

    /// <summary>
    /// Add user to the blog blacklist
    /// </summary>
    Task<User> Create(Guid blogId, string username);

    /// <summary>
    /// Remove user from the blacklist
    /// </summary>
    Task Delete(Guid blogId, string username);
}
