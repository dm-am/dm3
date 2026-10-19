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
            // A key is never rewritten, so a cached object cannot go stale.
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
            // Already gone: callers delete more than once by design.
            return true;
        }
        catch (OperationCanceledException)
        {
            // Shutdown, not a refusal: the caller decides.
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete {Key}", key);
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

            // The caller disposes the stream. Length is the store's, not the upload row's.
            return new StoredObject(
                response.ResponseStream,
                response.Headers.ContentType ?? "application/octet-stream",
                response.ContentLength >= 0 ? response.ContentLength : null);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden)
        {
            // Both are "no bytes here" to a caller that has already allowed the request.
            _logger.LogWarning(ex, "Object {Key} is not readable", key);
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
