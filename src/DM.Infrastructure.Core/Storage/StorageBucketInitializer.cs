using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Uploads;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DM.Infrastructure.Core.Storage;

/// <summary>
/// Startup-time idempotent: creates the S3 bucket if it does not exist yet and
/// asserts the read policy on it. Useful on a fresh reset+seed in dev — the MinIO
/// volume is clean and the bucket must be created before the first upload tries
/// to PUT anything there.
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
            if (!exists)
            {
                await _s3.PutBucketAsync(new PutBucketRequest
                {
                    BucketName = _cdn.BucketName,
                }, cancellationToken);
                _logger.LogInformation("S3 bucket {BucketName} created", _cdn.BucketName);
            }

            // Asserted on every start, not only on the branch that creates the
            // bucket: a bucket that already exists is exactly the one whose policy
            // is out of date, and a correction that only reaches fresh volumes
            // never reaches anything that has been running.
            await _s3.PutBucketPolicyAsync(new PutBucketPolicyRequest
            {
                BucketName = _cdn.BucketName,
                Policy = PublicReadPolicy(_cdn.BucketName, _cdn.Folder),
            }, cancellationToken);
            _logger.LogInformation(
                "S3 bucket {BucketName} policy asserted for prefixes {Prefixes}",
                _cdn.BucketName, string.Join(", ", PublicPrefixes(_cdn.Folder)));
        }
        catch (System.Exception ex)
        {
            // Do not fail — if the bucket cannot be created (IAM / network), the API still
            // starts. The first PUT will then fail with a clear message.
            _logger.LogWarning(ex, "Failed to initialize S3 bucket {BucketName}", _cdn.BucketName);
        }
    }

    /// <summary>
    /// Anonymous GET for CDN-style serving of the prefixes that are public by
    /// product decision, and only those.
    /// </summary>
    /// <remarks>
    /// The whole bucket used to be readable, which was the same statement while
    /// avatars were the only thing in it. It stops being the same statement the
    /// day a private upload type gets a producer, and the failure mode of getting
    /// that wrong is silent: a correct-looking URL that anyone can replay for as
    /// long as the object exists. Enumerating the prefixes makes the default for
    /// a new type "not readable".
    /// </remarks>
    /// <param name="bucketName">Bucket the policy applies to.</param>
    /// <param name="folder">Relative destination prefix, empty when objects sit at the root.</param>
    internal static string PublicReadPolicy(string bucketName, string folder)
    {
        var resources = string.Join(",\n                    ", PublicPrefixes(folder)
            .Select(prefix => $"\"arn:aws:s3:::{bucketName}/{prefix}/*\""));

        return $$"""
                {
                    "Version": "2012-10-17",
                    "Statement": [{
                        "Effect": "Allow",
                        "Principal": "*",
                        "Action": "s3:GetObject",
                        "Resource": [
                    {{resources}}
                        ]
                    }]
                }
                """;
    }

    private static IEnumerable<string> PublicPrefixes(string folder) =>
        UploadFolder.AnonymouslyReadable
            .Select(UploadFolder.For)
            .Select(prefix => string.IsNullOrEmpty(folder) ? prefix : $"{folder}/{prefix}");

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
