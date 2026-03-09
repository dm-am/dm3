using System;

namespace DM.Domain.Core.Caching;

/// <summary>
/// Unified cache TTL policies for the application.
/// All cache durations should be defined here for consistency.
/// </summary>
public static class CachePolicy
{
    /// <summary>
    /// Data that almost never changes (tags, schemas).
    /// Invalidated manually when changed.
    /// </summary>
    public static readonly TimeSpan Permanent = TimeSpan.FromHours(24);

    /// <summary>
    /// Data that changes rarely (popular games, boards structure).
    /// Refreshed periodically to pick up changes.
    /// </summary>
    public static readonly TimeSpan LongLived = TimeSpan.FromHours(1);

    /// <summary>
    /// Data that changes moderately (game lists for anonymous users).
    /// Short enough to show new games reasonably quickly.
    /// </summary>
    public static readonly TimeSpan Medium = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Data with user-specific counters (own games, unread counts).
    /// Short TTL as a balance between freshness and performance.
    /// </summary>
    public static readonly TimeSpan Short = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Frontend stale-while-revalidate window.
    /// After this time, cached data is returned immediately but refreshed in background.
    /// </summary>
    public static readonly TimeSpan FrontendStale = TimeSpan.FromSeconds(60);
}
