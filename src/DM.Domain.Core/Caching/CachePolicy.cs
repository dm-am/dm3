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
    /// Per-viewer data rebuilt on almost every request (game lists of an
    /// authenticated user). Long enough to absorb a burst of identical requests,
    /// short enough for the viewer not to notice the entry at all.
    /// </summary>
    public static readonly TimeSpan VeryShort = TimeSpan.FromSeconds(15);
}
