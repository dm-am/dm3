using Amazon.Runtime;
using Amazon.S3;
using DM.Domain.Core.Configuration;
using Microsoft.Extensions.Options;

namespace DM.Infrastructure.Core.Configuration;

internal class SelfHostedS3ClientProvider(IOptions<CdnConfiguration> cdnConfiguration) : IAmazonS3ClientProvider
{
    private readonly CdnConfiguration _cdn = cdnConfiguration.Value;

    public IAmazonS3 GetClient()
    {
        return new AmazonS3Client(
            new BasicAWSCredentials(_cdn.AccessKey, _cdn.SecretKey),
            new AmazonS3Config
            {
                ServiceURL = _cdn.Url,
                AuthenticationRegion = _cdn.Region,
                ForcePathStyle = true
            });
    }

    public bool CanBeUsed() => _cdn.Provider == S3Provider.SelfHosted;
}
