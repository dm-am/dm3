using System;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using DM.Domain.Core.Configuration;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DM.Infrastructure.Core.Tests.Configuration;

// The local store answers only path-style addresses, CloudCube is an AWS bucket behind its region's
// endpoint; asserted on the client the container hands out.
public class S3ClientSelectionShould
{
    private const string Endpoint = "http://storage.test:9000";

    [Fact]
    public void ReachTheSelfHostedStoreByPathWithTheRegionItIsTold()
    {
        var config = ClientFor(S3Provider.SelfHosted, "us-east-1");

        config.ForcePathStyle.Should().BeTrue(
            "the self-hosted store resolves the bucket from the path, and a virtual-host " +
            "address names a host that does not exist");
        config.AuthenticationRegion.Should().Be("us-east-1");
        config.ServiceURL.Should().StartWith(Endpoint);
    }

    [Fact]
    public void SendTheSelfHostedStoreChecksumsOnlyWhereAnOperationRequiresThem()
    {
        var config = ClientFor(S3Provider.SelfHosted, "us-east-1");

        config.RequestChecksumCalculation.Should().Be(RequestChecksumCalculation.WHEN_REQUIRED,
            "the aws-chunked trailer SDK 4 sends by default is a body the store was never checked with");
        config.ResponseChecksumValidation.Should().Be(ResponseChecksumValidation.WHEN_REQUIRED);
    }

    [Fact]
    public void ReachCloudCubeThroughTheEndpointOfItsRegion()
    {
        var config = ClientFor(S3Provider.CloudCube, "eu-west-1");

        config.RegionEndpoint.Should().Be(RegionEndpoint.EUWest1);
        config.ForcePathStyle.Should().BeFalse("an AWS bucket is addressed by its own host name");
    }

    // The default member is one no implementation claims.
    [Fact]
    public void RefuseAProviderLeftUnset()
    {
        var resolve = () => ClientFor(provider: null, "eu-west-1");

        resolve.Should().Throw<InvalidOperationException>()
            .WithMessage($"*CdnConfiguration:Provider = {default(S3Provider)}*");
    }

    private static AmazonS3Config ClientFor(S3Provider? provider, string region)
    {
        var services = new ServiceCollection();
        services.Configure<CdnConfiguration>(cdn =>
        {
            if (provider is not null)
            {
                cdn.Provider = provider.Value;
            }

            cdn.Url = Endpoint;
            cdn.Region = region;
            cdn.AccessKey = "key";
            cdn.SecretKey = "secret";
            cdn.BucketName = "dm-uploads";
        });
        services.AddDmCore();

        using var container = services.BuildServiceProvider();
        var client = container.GetRequiredService<IAmazonS3>();

        return (AmazonS3Config)client.Config;
    }
}
