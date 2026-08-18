using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Uploads;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DM.Infrastructure.Core.Storage;

/// <inheritdoc />
internal class ObjectStorage : IObjectStorage
{
    private readonly IAmazonS3 _s3;
    private readonly CdnConfiguration _cdn;
    private readonly ILogger<ObjectStorage> _logger;

    /// <inheritdoc />
    public ObjectStorage(
        IAmazonS3 s3,
        IOptions<CdnConfiguration> cdn,
        ILogger<ObjectStorage> logger)
    {
        _s3 = s3;
        _cdn = cdn.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task PutAsync(string key, byte[] content, string contentType,
        CancellationToken cancellationToken = default) =>
        _s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _cdn.BucketName,
            Key = key,
            InputStream = new MemoryStream(content, writable: false),
            ContentType = contentType,
            // A key is never rewritten — replacing an avatar allocates a new one —
            // so a cached object cannot go stale, and browsers and the CDN may
            // keep it forever.
            Headers =
            {
                CacheControl = "public, max-age=31536000, immutable",
            },
        }, cancellationToken);

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await _s3.DeleteObjectAsync(new DeleteObjectRequest
            {
                BucketName = _cdn.BucketName,
                Key = key,
            }, cancellationToken);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // Already gone. Every caller here deletes more than once by design —
            // a compensating rollback, a sweeper retrying the same row — so the
            // absent key is the expected end state, not a failure.
            return true;
        }
        catch (OperationCanceledException)
        {
            // Shutdown, not a refusal: the caller decides, and swallowing it here
            // would report a key as removed while nothing was even attempted.
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Object storage] Failed to delete {Key}", key);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<StoredObject?> OpenReadAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _s3.GetObjectAsync(new GetObjectRequest
            {
                BucketName = _cdn.BucketName,
                Key = key,
            }, cancellationToken);

            // ResponseStream keeps the response alive; handing it over means the
            // caller disposes it. Length is what the store reports, not what the
            // upload row remembers: the row is a copy and this is the object.
            return new StoredObject(
                response.ResponseStream,
                response.Headers.ContentType ?? "application/octet-stream",
                response.ContentLength >= 0 ? response.ContentLength : null);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden)
        {
            // A key the bucket does not hold, or one this account may not read.
            // Both are "there are no bytes here" to a caller that has already
            // decided the request is allowed, and neither is worth an exception
            // travelling up through a controller that would answer 404 anyway.
            _logger.LogWarning(ex, "[Object storage] Object {Key} is not readable", key);
            return null;
        }
    }

    /// <inheritdoc />
    public string BuildPublicUrl(string key) =>
        new UriBuilder(new Uri(_cdn.PublicUrl))
        {
            Path = $"{_cdn.BucketName}/{key}",
        }.ToString();
}
