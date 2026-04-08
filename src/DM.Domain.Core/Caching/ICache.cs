using System;
using System.Threading.Tasks;

namespace DM.Domain.Core.Caching;

/// <summary>
/// Cache wrapper for different strategies
/// </summary>
public interface ICache
{
    /// <summary>
    /// Get cache entry or create new and return result (with default LongLived TTL)
    /// </summary>
    /// <param name="key">Cache key</param>
    /// <param name="create">Entry factory</param>
    /// <typeparam name="TEntry">Cache entry type</typeparam>
    /// <returns>Stored entry</returns>
    Task<TEntry> GetOrCreateAsync<TEntry>(object key, Func<Task<TEntry>> create);

    /// <summary>
    /// Get cache entry or create new and return result with specified TTL
    /// </summary>
    /// <param name="key">Cache key</param>
    /// <param name="create">Entry factory</param>
    /// <param name="absoluteExpiration">Time to live</param>
    /// <typeparam name="TEntry">Cache entry type</typeparam>
    /// <returns>Stored entry</returns>
    Task<TEntry> GetOrCreateAsync<TEntry>(object key, Func<Task<TEntry>> create, TimeSpan absoluteExpiration);

    /// <summary>
    /// Invalidate cache entry
    /// </summary>
    /// <param name="key">Cache key</param>
    Task InvalidateAsync(object key);
}
