using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Subscriptions;

namespace DM.Domain.Blog.Features.Subscriptions;

/// <summary>
/// Service for managing blog subscriptions (readers)
/// </summary>
public interface IBlogSubscriptionService
{
    /// <summary>
    /// Subscribe current user to a blog
    /// </summary>
    Task<Subscription> Subscribe(Guid blogId, CancellationToken ct = default);

    /// <summary>
    /// Unsubscribe current user from a blog
    /// </summary>
    Task Unsubscribe(Guid blogId, CancellationToken ct = default);

    /// <summary>
    /// Get readers (subscribers) of a blog
    /// </summary>
    Task<IEnumerable<GeneralUser>> GetReaders(Guid blogId, CancellationToken ct = default);

    /// <summary>
    /// Check if a user is subscribed to a blog
    /// </summary>
    Task<bool> IsSubscribed(Guid userId, Guid blogId, CancellationToken ct = default);
}
