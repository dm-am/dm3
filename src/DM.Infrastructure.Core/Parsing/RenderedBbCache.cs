using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Caching;

namespace DM.Infrastructure.Core.Parsing;

/// <summary>
/// Thin wrapper over <see cref="ICache"/> that caches rendered BBCode output
/// keyed by (source hash, audience, permission bucket). Identical inputs
/// from the same bucket always produce byte-identical output, so a single
/// cache entry serves every viewer in the bucket.
///
/// Invalidation is by source hash: when a post is edited, every cached
/// bucket for that source is evicted via the reverse index.
/// </summary>
public interface IRenderedBbCache
{
    /// <summary>
    /// Compute the short content hash used in cache keys. Used by callers
    /// that need to invalidate a specific source (e.g. on post edit) without
    /// re-rendering first.
    /// </summary>
    string ComputeSourceHash(string source);

    /// <summary>
    /// Fetch a rendered string from the cache or produce it via the factory.
    /// </summary>
    Task<string> GetOrRenderAsync(
        string sourceHash,
        RenderAudience audience,
        PermissionBucket bucket,
        Func<Task<string>> produce);

    /// <summary>
    /// Invalidate every cached variant of a source — called on edit.
    /// </summary>
    Task InvalidateSourceAsync(string sourceHash);
}

/// <inheritdoc />
internal sealed class RenderedBbCache : IRenderedBbCache
{
    private readonly ICache _cache;

    // Reverse index: sourceHash → full cache keys that exist for it.
    // Populated on every GetOrRenderAsync miss so InvalidateSourceAsync
    // can scan a bounded set instead of prefix-walking IMemoryCache.
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _sourceIndex = new();

    // Per-source stampede guard: concurrent misses for the same source
    // coalesce behind a single lock so the expensive parse+render only
    // runs once.
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _sourceLocks = new();

    /// <inheritdoc />
    public RenderedBbCache(ICache cache)
    {
        _cache = cache;
    }

    /// <inheritdoc />
    public string ComputeSourceHash(string source)
    {
        if (string.IsNullOrEmpty(source)) return "empty";
        Span<byte> buffer = stackalloc byte[32];
        var bytes = Encoding.UTF8.GetBytes(source);
        SHA256.HashData(bytes, buffer);
        var sb = new StringBuilder(16);
        for (var i = 0; i < 8; i++)
            sb.Append(buffer[i].ToString("x2"));
        return sb.ToString();
    }

    /// <inheritdoc />
    public async Task<string> GetOrRenderAsync(
        string sourceHash,
        RenderAudience audience,
        PermissionBucket bucket,
        Func<Task<string>> produce)
    {
        var key = BuildKey(sourceHash, audience, bucket);
        var gate = _sourceLocks.GetOrAdd(sourceHash, _ => new SemaphoreSlim(1, 1));
        var rendered = await _cache.GetOrCreateAsync(key, async () =>
        {
            await gate.WaitAsync().ConfigureAwait(false);
            try
            {
                return await produce().ConfigureAwait(false);
            }
            finally
            {
                gate.Release();
            }
        }, CachePolicy.LongLived).ConfigureAwait(false);

        var bucketSet = _sourceIndex.GetOrAdd(sourceHash, _ => new ConcurrentDictionary<string, byte>());
        bucketSet.TryAdd(key, 0);
        return rendered;
    }

    /// <inheritdoc />
    public async Task InvalidateSourceAsync(string sourceHash)
    {
        if (_sourceIndex.TryRemove(sourceHash, out var keys))
        {
            foreach (var key in keys.Keys)
                await _cache.InvalidateAsync(key).ConfigureAwait(false);
        }
        if (_sourceLocks.TryRemove(sourceHash, out var gate))
            gate.Dispose();
    }

    private static string BuildKey(string sourceHash, RenderAudience audience, PermissionBucket bucket)
    {
        var audienceKey = audience switch
        {
            RenderAudience.Display => "display",
            RenderAudience.AuthorEdit => "authoredit",
            RenderAudience.PlainText => "plain",
            RenderAudience.EmbedSafe => "embedsafe",
            _ => audience.ToString().ToLowerInvariant()
        };
        return $"bb:v1:{sourceHash}:{audienceKey}:{bucket.Key}";
    }
}

