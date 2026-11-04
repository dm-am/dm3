using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;

namespace DM.Domain.Blog.Features.Blogs;

/// <summary>
/// Service for the people of a blog: its readers and assistants, and the
/// reader's own moves on it
/// </summary>
public interface IBlogUserService
{
    /// <returns>The subscribed user</returns>
    Task<GeneralUser> Subscribe(Guid blogId, CancellationToken ct = default);

    Task Unsubscribe(Guid blogId, CancellationToken ct = default);

    /// <summary>
    /// Leave a blog (remove current user as assistant)
    /// </summary>
    Task Leave(Guid blogId, CancellationToken ct = default);

    Task<IEnumerable<UserReference>> GetReaders(Guid blogId, CancellationToken ct = default);

    Task<IEnumerable<BlogUser>> GetAssistants(Guid blogId, CancellationToken ct = default);

    Task RemoveAssistant(Guid blogId, string username, CancellationToken ct = default);
}
