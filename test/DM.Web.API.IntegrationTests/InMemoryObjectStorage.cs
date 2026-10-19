using System.Collections.Concurrent;
using DM.Domain.Core.Uploads;

namespace DM.Web.API.IntegrationTests;

/// <summary>
/// The object store, as a dictionary.
/// </summary>
/// <remarks>
/// Nothing here asserts S3's behaviour, only who reaches bytes that exist. Static: the host is shared across the
/// collection.
/// </remarks>
public sealed class InMemoryObjectStorage : IObjectStorage
{
    private static readonly ConcurrentDictionary<string, (byte[] Content, string ContentType)> Objects = new();

    /// <summary>Put bytes under a key without going through an upload.</summary>
    public static void Seed(string key, byte[] content, string contentType) =>
        Objects[key] = (content, contentType);

    /// <summary>Whether the store still holds the key.</summary>
    public static bool Holds(string key) => Objects.ContainsKey(key);

    /// <inheritdoc />
    public Task PutAsync(string key, byte[] content, string contentType,
        CancellationToken cancellationToken = default)
    {
        Objects[key] = (content, contentType);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        Objects.TryRemove(key, out _);
        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public Task<StoredObject?> OpenReadAsync(string key, CancellationToken cancellationToken = default)
    {
        if (!Objects.TryGetValue(key, out var stored))
        {
            return Task.FromResult<StoredObject?>(null);
        }

        return Task.FromResult<StoredObject?>(new StoredObject(
            new MemoryStream(stored.Content, writable: false),
            stored.ContentType,
            stored.Content.LongLength));
    }

    /// <inheritdoc />
    public string BuildPublicUrl(string key) => $"https://cdn.test/dm-test/{key}";
}
