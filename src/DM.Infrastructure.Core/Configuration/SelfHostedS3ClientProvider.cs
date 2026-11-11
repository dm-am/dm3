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
                ForcePathStyle = true,
                // SDK 4 by default sends a CRC32 trailer in an aws-chunked body and asks for
                // checksums on reads. Only the plain body of SDK 3 has been proven through nginx
                // and SeaweedFS, so checksums go only where an operation requires them.
                RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
            });
    }

    public bool CanBeUsed() => _cdn.Provider == S3Provider.SelfHosted;
}
