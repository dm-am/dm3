using System;
using System.Threading.Tasks;
using DM.Domain.Core.Caching;
using Microsoft.Extensions.Caching.Memory;

namespace DM.Infrastructure.Core.Caching;

/// <inheritdoc />
internal class MemoryCache : ICache
{
    private readonly IMemoryCache _memoryCache;

    /// <inheritdoc />
    public MemoryCache(
        IMemoryCache memoryCache)
    {
        _memoryCache = memoryCache;
    }

    /// <inheritdoc />
    public Task<TEntry> GetOrCreateAsync<TEntry>(object key, Func<Task<TEntry>> create) =>
        GetOrCreateAsync(key, create, CachePolicy.LongLived);

    /// <inheritdoc />
    public async Task<TEntry> GetOrCreateAsync<TEntry>(object key, Func<Task<TEntry>> create, TimeSpan absoluteExpiration) =>
        (await _memoryCache.GetOrCreateAsync(key, async e =>
        {
            e.AbsoluteExpirationRelativeToNow = absoluteExpiration;
            return await create();
        }))!;

    /// <inheritdoc />
    public Task InvalidateAsync(object key)
    {
        _memoryCache.Remove(key);
        return Task.CompletedTask;
    }
}