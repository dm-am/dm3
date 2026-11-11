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
                // SDK 3 sent a signed aws-chunked body without a trailer. SDK 4 by default adds a
                // CRC32 trailer to that body and asks for checksums on reads; WHEN_REQUIRED keeps
                // the SDK 3 body. SDK 4 still adds "Content-Encoding: aws-chunked", which
                // SeaweedFS 4.53 drops when it stores the object.
                RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
            });
    }

    public bool CanBeUsed() => _cdn.Provider == S3Provider.SelfHosted;
}
