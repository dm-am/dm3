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
/// Startup-time идемпотент: создает bucket в S3, если он еще не существует.
/// Полезно при свежем reset+seed в dev — MinIO volume чист и bucket надо
/// создать до того, как первый upload попробует туда что-то PUT'нуть.
///
/// В production buckets обычно создаются через IaC (Terraform/CDK), но
/// running этого тоже идемпотент — лишним не будет.
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

            // Анонимный GET для CDN-style раздачи аватаров (никаких presigned-
            // URLs для публичного контента). Полностью симметрично production-
            // CDN: bucket policy = read-only для всех.
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
            // Не падаем — если bucket не создается (IAM / network), API все
            // равно поднимется. Первый PUT тогда упадет с ясным сообщением.
            _logger.LogWarning(ex, "Failed to initialize S3 bucket {BucketName}", _cdn.BucketName);
        }
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
