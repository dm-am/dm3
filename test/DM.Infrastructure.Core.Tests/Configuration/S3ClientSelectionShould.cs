using System;
using Amazon;
using Amazon.S3;
using DM.Domain.Core.Configuration;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DM.Infrastructure.Core.Tests.Configuration;

/// <summary>
/// The object storage client is built for the provider the configuration names.
/// </summary>
/// <remarks>
/// The two providers want different clients from the same settings: the store
/// run beside the site answers only path-style addresses and signs for whatever
/// region it is told, while CloudCube is an AWS bucket reached through its
/// region's endpoint. The one chosen for the other provider starts without a
/// complaint and fails on the first upload, so the choice is asserted on the
/// client the container hands out rather than on the settings it was given.
/// </remarks>
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
    public void ReachCloudCubeThroughTheEndpointOfItsRegion()
    {
        var config = ClientFor(S3Provider.CloudCube, "eu-west-1");

        config.RegionEndpoint.Should().Be(RegionEndpoint.EUWest1);
        config.ForcePathStyle.Should().BeFalse("an AWS bucket is addressed by its own host name");
    }

    /// <summary>
    /// A host that leaves the provider unset is refused with the setting and its value named.
    /// </summary>
    /// <remarks>
    /// The default member of the setting is one no implementation claims, so a
    /// host that leaves it unset lands here, and a bare "Sequence contains no
    /// matching element" names neither.
    /// </remarks>
    [Fact]
    public void RefuseAProviderLeftUnset()
    {
        var resolve = () => ClientFor(provider: null, "eu-west-1");

        resolve.Should().Throw<InvalidOperationException>()
            .WithMessage($"*CdnConfiguration:Provider = {default(S3Provider)}*");
    }

    /// <summary>The client the container builds, with the provider left unset when none is given.</summary>
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
