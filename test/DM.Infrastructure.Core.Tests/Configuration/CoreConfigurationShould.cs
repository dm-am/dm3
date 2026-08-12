using System;
using System.Collections.Generic;
using DM.Infrastructure.Core.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace DM.Infrastructure.Core.Tests.Configuration;

/// <summary>
/// A host is refused what it cannot work with, and asked for nothing else.
/// </summary>
/// <remarks>
/// The shared call demanded both connection strings from every host, so the mail
/// worker - which reads a queue and speaks SMTP - could not start without a Postgres
/// and a Mongo it never opens, and the next host would have been pushed into writing
/// a fake value, which is what empties the check for the hosts that need it. The
/// imgproxy pair had the opposite problem: nothing validated it, a non-hex value
/// threw on the first page with a thumbnail, and a key with no salt turned every
/// thumbnail into a 403 with no line in any log.
///
/// Validation runs where the host runs it, through the startup validator, because
/// binding alone never fails.
/// </remarks>
public class CoreConfigurationShould
{
    private static readonly Dictionary<string, string?> Storage = new()
    {
        ["ConnectionStrings:Rdb"] = "Host=localhost;Database=dm3",
        ["ConnectionStrings:Mongo"] = "mongodb://localhost:27017/dm3",
        ["CdnConfiguration:Url"] = "http://localhost:9000",
        ["CdnConfiguration:BucketName"] = "dm-uploads",
        ["CdnConfiguration:AccessKey"] = "key",
        ["CdnConfiguration:SecretKey"] = "secret",
        ["SiteAddressConfiguration:PublicUrl"] = "http://localhost:5173",
    };

    [Fact]
    public void AskNoStoreOfAHostThatDeclaresNone() =>
        Validate(new Dictionary<string, string?> { ["SiteAddressConfiguration:PublicUrl"] = "http://localhost:5173" })
            .Should().BeNull(
                "a worker that opens neither store must be able to start without naming " +
                "either, or the demand stops meaning anything for the hosts that do");

    [Fact]
    public void RefuseAHostThatDeclaresTheRelationalStoreWithoutOne() =>
        Validate(
                new Dictionary<string, string?> { ["SiteAddressConfiguration:PublicUrl"] = "http://localhost:5173" },
                services => services.RequireRelationalStorage())
            .Should().Contain("ConnectionStrings:Rdb");

    [Fact]
    public void RefuseAHostThatDeclaresTheDocumentStoreWithoutOne() =>
        Validate(
                new Dictionary<string, string?> { ["SiteAddressConfiguration:PublicUrl"] = "http://localhost:5173" },
                services => services.RequireDocumentStorage())
            .Should().Contain("ConnectionStrings:Mongo");

    [Fact]
    public void RefuseAHostThatDeclaresObjectStorageWithoutCredentials() =>
        Validate(
                new Dictionary<string, string?>
                {
                    ["SiteAddressConfiguration:PublicUrl"] = "http://localhost:5173",
                    ["CdnConfiguration:Url"] = "http://localhost:9000",
                },
                services => services.RequireObjectStorage())
            .Should().Contain("CdnConfiguration");

    [Fact]
    public void AcceptAnUnsignedImageProxy() =>
        Validate(Storage).Should().BeNull(
            "an empty key and an empty salt together are the local setup imgproxy runs " +
            "with insecure URLs enabled");

    [Fact]
    public void RefuseASigningKeyThatIsNotHex() =>
        Validate(With("ImageProxyConfiguration:Key", "not-hex-at-all", "ImageProxyConfiguration:Salt", "ab12"))
            .Should().Contain("ImageProxyConfiguration",
                "the builder converts it from hex in its constructor, so a base64 value " +
                "throws on the first page with an avatar rather than at startup");

    [Fact]
    public void RefuseAKeyWithoutASalt() =>
        Validate(With("ImageProxyConfiguration:Key", "ab12", "ImageProxyConfiguration:Salt", ""))
            .Should().Contain("ImageProxyConfiguration",
                "half a pair signs nothing: the application emits /insecure/ URLs and an " +
                "imgproxy holding a key answers 403 to every one of them");

    private static Dictionary<string, string?> With(string firstKey, string firstValue, string secondKey, string secondValue)
    {
        var settings = new Dictionary<string, string?>(Storage)
        {
            [firstKey] = firstValue,
            [secondKey] = secondValue,
        };
        return settings;
    }

    /// <summary>The message of the first failure, or null when the host would start.</summary>
    private static string? Validate(
        IDictionary<string, string?> settings, Action<IServiceCollection>? declare = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddOptions().AddDmCoreConfiguration(configuration);
        declare?.Invoke(services);

        using var provider = services.BuildServiceProvider();
        try
        {
            provider.GetRequiredService<IStartupValidator>().Validate();
            return null;
        }
        catch (OptionsValidationException exception)
        {
            return exception.Message;
        }
    }
}
