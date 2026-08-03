using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;

namespace DM.Domain.Blog.Features.Blacklists;

/// <summary>
/// Service for blog blacklist management
/// </summary>
public interface IBlogBlacklistService
{
    /// <summary>
    /// Get list of blacklisted users for the blog
    /// </summary>
    Task<IEnumerable<GeneralUser>> Get(Guid blogId, CancellationToken ct = default);

    /// <summary>
    /// Add user to the blog blacklist
    /// </summary>
    /// <param name="dto">DTO with blog ID and username</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Blacklisted user</returns>
    Task<GeneralUser> Add(OperateBlogBlacklistLink dto, CancellationToken ct = default);

    /// <summary>
    /// Remove user from the blog blacklist
    /// </summary>
    /// <param name="dto">DTO with blog ID and username</param>
    /// <param name="ct">Cancellation token</param>
    Task Remove(OperateBlogBlacklistLink dto, CancellationToken ct = default);
}
