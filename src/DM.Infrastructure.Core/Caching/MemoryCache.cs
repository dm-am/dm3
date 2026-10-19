using System;
using System.Threading.Tasks;
using DM.Domain.Core.Caching;
using DM.Infrastructure.Core.Tracing;
using Microsoft.Extensions.Caching.Memory;

namespace DM.Infrastructure.Core.Caching;

/// <inheritdoc />
internal class MemoryCache : ICache
{
    private readonly IMemoryCache _memoryCache;

    public MemoryCache(
        IMemoryCache memoryCache)
    {
        _memoryCache = memoryCache;
    }

    /// <inheritdoc />
    public Task<TEntry> GetOrCreateAsync<TEntry>(object key, Func<Task<TEntry>> create) =>
        GetOrCreateAsync(key, create, CachePolicy.LongLived);

    /// <inheritdoc />
    public async Task<TEntry> GetOrCreateAsync<TEntry>(object key, Func<Task<TEntry>> create, TimeSpan absoluteExpiration)
    {
        // The factory runs only on a miss.
        var miss = false;
        var entry = (await _memoryCache.GetOrCreateAsync(key, async e =>
        {
            miss = true;
            e.AbsoluteExpirationRelativeToNow = absoluteExpiration;
            return await create();
        }))!;

        CacheMetrics.Requests.Add(1, CacheMetrics.Result(!miss), CacheMetrics.Entry(EntryName<TEntry>()));
        return entry;
    }

    /// <summary>
    /// The type name without the arity of a generic, not "ValueTuple`2".
    /// </summary>
    private static string EntryName<TEntry>()
    {
        var name = typeof(TEntry).Name;
        var arity = name.IndexOf('`');

        return arity < 0 ? name : name[..arity];
    }

    /// <inheritdoc />
    public Task InvalidateAsync(object key)
    {
        _memoryCache.Remove(key);
        return Task.CompletedTask;
    }
}
