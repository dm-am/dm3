using System;
using System.Linq;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Uploads;
using DM.Infrastructure.Core.Storage;
using FluentAssertions;
using Xunit;

namespace DM.Infrastructure.Core.Tests.Storage;

/// <summary>
/// The bucket policy is the only thing standing between a stored object and
/// anyone who has its address, and it is written once, at startup, where no
/// request will ever exercise it.
/// </summary>
/// <remarks>
/// It used to grant anonymous reads on the whole bucket with a comment about
/// avatars — the same statement while avatars were the only producer, and a
/// silent decision about every type added after them. Enumerating the readable
/// prefixes is what makes a new type private by default; this asserts that the
/// enumeration is what the policy is built from, so a type left out of the public
/// list cannot be read by the world.
/// </remarks>
public class StorageBucketPolicyShould
{
    private const string Bucket = "dm-uploads";

    [Fact]
    public void GrantAnonymousReadsOnThePublicPrefixesOnly()
    {
        var policy = StorageBucketInitializer.PublicReadPolicy(Bucket, folder: string.Empty);

        policy.Should().Contain($"arn:aws:s3:::{Bucket}/avatars/*");
        policy.Should().Contain($"arn:aws:s3:::{Bucket}/characters/*");
        policy.Should().NotContain($"arn:aws:s3:::{Bucket}/*",
            "a bucket-wide grant says nothing about which types were meant to be public");
    }

    /// <summary>
    /// The point of the change, stated over the enum rather than over today's
    /// values: a type nobody declared public must not appear in the policy.
    /// </summary>
    [Fact]
    public void LeaveEveryTypeThatIsNotDeclaredPublicOutOfThePolicy()
    {
        var policy = StorageBucketInitializer.PublicReadPolicy(Bucket, folder: string.Empty);

        var privateTypes = Enum.GetValues<UploadType>()
            .Except(UploadFolder.AnonymouslyReadable)
            .ToArray();

        privateTypes.Should().NotBeEmpty("otherwise this asserts nothing");
        foreach (var type in privateTypes)
        {
            policy.Should().NotContain($"/{UploadFolder.For(type)}/",
                $"{type} was never declared anonymously readable");
        }
    }

    /// <summary>
    /// Objects sit under the configured destination prefix, so the grant has to
    /// name the same path the keys are written to.
    /// </summary>
    [Fact]
    public void FollowTheConfiguredDestinationPrefix()
    {
        var policy = StorageBucketInitializer.PublicReadPolicy(Bucket, folder: "dm");

        policy.Should().Contain($"arn:aws:s3:::{Bucket}/dm/avatars/*");
        policy.Should().NotContain($"arn:aws:s3:::{Bucket}/avatars/*");
    }
}
