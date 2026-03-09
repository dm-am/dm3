using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Blacklists;
using DM.Domain.Core.Dto;

namespace DM.Domain.Blog.Features.Blacklists;

/// <summary>
/// Service for blog blacklist management
/// </summary>
public interface IBlogBlacklistService : IContentBlacklistService
{
    /// <summary>
    /// Get list of blacklisted users for the blog
    /// </summary>
    Task<IEnumerable<GeneralUser>> Get(Guid blogId, CancellationToken ct = default);

    /// <summary>
    /// Add user to the blog blacklist
    /// </summary>
    Task<GeneralUser> Add(Guid blogId, string username, CancellationToken ct = default);

    /// <summary>
    /// Remove user from the blog blacklist
    /// </summary>
    Task Remove(Guid blogId, string username, CancellationToken ct = default);
}
