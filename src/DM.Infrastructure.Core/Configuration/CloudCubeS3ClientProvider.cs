using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using DM.Domain.Core.Configuration;
using Microsoft.Extensions.Options;

namespace DM.Infrastructure.Core.Configuration;

internal class CloudCubeS3ClientProvider(IOptions<CdnConfiguration> cdnConfiguration) : IAmazonS3ClientProvider
{
    private readonly CdnConfiguration _cdn = cdnConfiguration.Value;

    public IAmazonS3 GetClient() => new AmazonS3Client(
        new BasicAWSCredentials(_cdn.AccessKey, _cdn.SecretKey),
        new AmazonS3Config
        {
            ServiceURL = _cdn.Url,
            RegionEndpoint = RegionEndpoint.GetBySystemName(_cdn.Region)
        });

    public bool CanBeUsed() => _cdn.Provider == S3Provider.CloudCube;
}
