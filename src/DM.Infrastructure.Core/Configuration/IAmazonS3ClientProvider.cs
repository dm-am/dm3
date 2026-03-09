using Amazon.S3;

namespace DM.Infrastructure.Core.Configuration;

internal interface IAmazonS3ClientProvider
{
    IAmazonS3 GetClient();
    bool CanBeUsed();
}