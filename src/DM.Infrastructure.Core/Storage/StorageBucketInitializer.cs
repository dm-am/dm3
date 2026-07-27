using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using DM.Domain.Core.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DM.Infrastructure.Core.Storage;

/// <summary>
/// Startup-time idempotent: creates the S3 bucket if it does not exist yet.
/// Useful on a fresh reset+seed in dev — the MinIO volume is clean and the bucket must
/// be created before the first upload tries to PUT anything there.
///
/// In production buckets are usually created via IaC (Terraform/CDK), but
/// running this is idempotent too — it does no harm.
/// </summary>
public class StorageBucketInitializer : IHostedService
{
    private readonly IAmazonS3 _s3;
    private readonly CdnConfiguration _cdn;
    private readonly ILogger<StorageBucketInitializer> _logger;

    /// <inheritdoc />
    public StorageBucketInitializer(
        IAmazonS3 s3,
        IOptions<CdnConfiguration> cdn,
        ILogger<StorageBucketInitializer> logger)
    {
        _s3 = s3;
        _cdn = cdn.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var exists = await AmazonS3Util.DoesS3BucketExistV2Async(_s3, _cdn.BucketName);
            if (exists)
            {
                _logger.LogDebug("S3 bucket {BucketName} already exists", _cdn.BucketName);
                return;
            }

            await _s3.PutBucketAsync(new PutBucketRequest
            {
                BucketName = _cdn.BucketName,
            }, cancellationToken);
            _logger.LogInformation("S3 bucket {BucketName} created", _cdn.BucketName);

            // Anonymous GET for CDN-style avatar serving (no presigned
            // URLs for public content). Fully symmetric with a production
            // CDN: bucket policy = read-only for everyone.
            await _s3.PutBucketPolicyAsync(new PutBucketPolicyRequest
            {
                BucketName = _cdn.BucketName,
                Policy = $$"""
                {
                    "Version": "2012-10-17",
                    "Statement": [{
                        "Effect": "Allow",
                        "Principal": "*",
                        "Action": "s3:GetObject",
                        "Resource": "arn:aws:s3:::{{_cdn.BucketName}}/*"
                    }]
                }
                """,
            }, cancellationToken);
            _logger.LogInformation("S3 bucket {BucketName} policy set to public read", _cdn.BucketName);
        }
        catch (System.Exception ex)
        {
            // Do not fail — if the bucket cannot be created (IAM / network), the API still
            // starts. The first PUT will then fail with a clear message.
            _logger.LogWarning(ex, "Failed to initialize S3 bucket {BucketName}", _cdn.BucketName);
        }
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
