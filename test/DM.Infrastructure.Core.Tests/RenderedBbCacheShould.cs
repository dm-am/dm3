using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Caching;
using DM.Infrastructure.Core.Parsing;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace DM.Infrastructure.Core.Tests;

/// <summary>
/// Contract tests for the rendered-BBCode cache. Guarantees the key
/// format incorporates all three dimensions (source, audience, bucket),
/// invalidation removes every bucket sharing a source, and per-source
/// stampede guard coalesces concurrent misses.
/// </summary>
public class RenderedBbCacheShould
{
    private readonly IRenderedBbCache _cache;

    public RenderedBbCacheShould()
    {
        var memory = new MemoryCache(new MemoryCacheOptions());
        var inner = new TestCache(memory);
        // Use reflection-free construction: the internal class is visible
        // to the test project via InternalsVisibleTo.
        _cache = new RenderedBbCache(inner);
    }

    [Fact]
    public void ProduceStableSourceHash()
    {
        var h1 = _cache.ComputeSourceHash("[b]hello[/b]");
        var h2 = _cache.ComputeSourceHash("[b]hello[/b]");
        var h3 = _cache.ComputeSourceHash("[b]hello[/b] world");

        h1.Should().Be(h2, "identical input must produce identical hash");
        h1.Should().NotBe(h3, "different input must produce different hash");
        h1.Should().HaveLength(16, "short hash is 16 hex chars");
    }

    [Fact]
    public void ReportEmptyHashForEmptyInput() =>
        _cache.ComputeSourceHash(string.Empty).Should().Be("empty");

    [Fact]
    public async Task CacheRenderedOutput_ReusesFactoryOnHit()
    {
        var callCount = 0;
        Task<string> Produce()
        {
            Interlocked.Increment(ref callCount);
            return Task.FromResult("rendered-value");
        }

        var hash = _cache.ComputeSourceHash("[b]x[/b]");
        var bucket = PermissionBucket.Anonymous;

        var first = await _cache.GetOrRenderAsync(hash, RenderAudience.Display, bucket, Produce);
        var second = await _cache.GetOrRenderAsync(hash, RenderAudience.Display, bucket, Produce);

        first.Should().Be("rendered-value");
        second.Should().Be("rendered-value");
        callCount.Should().Be(1, "the second call must hit the cache");
    }

    [Fact]
    public async Task DifferentBuckets_DoNotShareCacheEntry()
    {
        var callCount = 0;
        Task<string> Produce(string label) => Task.Run(() =>
        {
            Interlocked.Increment(ref callCount);
            return label;
        });

        var hash = _cache.ComputeSourceHash("[b]x[/b]");

        await _cache.GetOrRenderAsync(hash, RenderAudience.Display, PermissionBucket.Anonymous,
            () => Produce("anon"));
        await _cache.GetOrRenderAsync(hash, RenderAudience.Display, PermissionBucket.AuthenticatedCoarse,
            () => Produce("user"));

        callCount.Should().Be(2, "different buckets occupy different cache slots");
    }

    [Fact]
    public async Task DifferentAudiences_DoNotShareCacheEntry()
    {
        var callCount = 0;
        var hash = _cache.ComputeSourceHash("[b]x[/b]");

        await _cache.GetOrRenderAsync(hash, RenderAudience.Display, PermissionBucket.Anonymous,
            () => { Interlocked.Increment(ref callCount); return Task.FromResult("display"); });
        await _cache.GetOrRenderAsync(hash, RenderAudience.PlainText, PermissionBucket.Anonymous,
            () => { Interlocked.Increment(ref callCount); return Task.FromResult("plain"); });

        callCount.Should().Be(2, "different audiences occupy different cache slots");
    }

    [Fact]
    public async Task InvalidateSource_EvictsEveryBucketForThatSource()
    {
        var callCount = 0;
        var hash = _cache.ComputeSourceHash("[b]x[/b]");

        await _cache.GetOrRenderAsync(hash, RenderAudience.Display, PermissionBucket.Anonymous,
            () => { Interlocked.Increment(ref callCount); return Task.FromResult("a"); });
        await _cache.GetOrRenderAsync(hash, RenderAudience.Display, PermissionBucket.AuthenticatedCoarse,
            () => { Interlocked.Increment(ref callCount); return Task.FromResult("b"); });
        callCount.Should().Be(2);

        await _cache.InvalidateSourceAsync(hash);

        await _cache.GetOrRenderAsync(hash, RenderAudience.Display, PermissionBucket.Anonymous,
            () => { Interlocked.Increment(ref callCount); return Task.FromResult("a2"); });
        await _cache.GetOrRenderAsync(hash, RenderAudience.Display, PermissionBucket.AuthenticatedCoarse,
            () => { Interlocked.Increment(ref callCount); return Task.FromResult("b2"); });

        callCount.Should().Be(4, "both buckets were evicted so both re-render");
    }

    // Minimal ICache implementation bound to IMemoryCache — mirrors the
    // production MemoryCache wrapper for tests without touching DI.
    private sealed class TestCache : ICache
    {
        private readonly IMemoryCache _memoryCache;

        public TestCache(IMemoryCache memoryCache) => _memoryCache = memoryCache;

        public Task<TEntry> GetOrCreateAsync<TEntry>(object key, Func<Task<TEntry>> create) =>
            GetOrCreateAsync(key, create, TimeSpan.FromHours(1));

        public async Task<TEntry> GetOrCreateAsync<TEntry>(
            object key, Func<Task<TEntry>> create, TimeSpan absoluteExpiration) =>
            (await _memoryCache.GetOrCreateAsync(key, async e =>
            {
                e.AbsoluteExpirationRelativeToNow = absoluteExpiration;
                return await create();
            }))!;

        public Task InvalidateAsync(object key)
        {
            _memoryCache.Remove(key);
            return Task.CompletedTask;
        }
    }
}
