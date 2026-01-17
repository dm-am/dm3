using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;

namespace DM.Services.Core.Caching;

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
    public Task<TEntry> GetOrCreate<TEntry>(object key, Func<Task<TEntry>> create) =>
        _memoryCache.GetOrCreateAsync(key, _ => create());

    /// <inheritdoc />
    public Task Invalidate(object key)
    {
        _memoryCache.Remove(key);
        return Task.CompletedTask;
    }
}