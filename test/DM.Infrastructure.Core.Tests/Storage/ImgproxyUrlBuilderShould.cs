using DM.Domain.Core.Configuration;
using DM.Infrastructure.Core.Storage;
using AwesomeAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace DM.Infrastructure.Core.Tests.Storage;

/// <summary>
/// Unit tests for <see cref="ImgproxyUrlBuilder"/> — HMAC-SHA256 signature,
/// base64url encoding, options payload (resize+gravity+quality).
///
/// The known hex key/salt from the imgproxy docs:
///   key  = 943b421c0089cdf87d15bd24210d8df80abf7e8aa3a4f96f5c4f7d5e0c8b6a93
///   salt = 520f986bbb1d7c4f9e1a6c83b5d0f47891234567890abcdef0123456789abcdef
/// The same pair sits in .env / docker-compose for dev, so the tests are
/// deterministic with it.
/// </summary>
public class ImgproxyUrlBuilderShould
{
    private const string TestKey = "943b421c0089cdf87d15bd24210d8df80abf7e8aa3a4f96f5c4f7d5e0c8b6a93";
    private const string TestSalt = "520f986bbb1d7c4f9e1a6c83b5d0f4789e123456789abcdef0123456789abcde";
    private const string TestEndpoint = "http://imgproxy.local";
    private const string TestPrefix = "s3://dm-uploads/";

    private static ImgproxyUrlBuilder CreateBuilder(string? key = TestKey, string? salt = TestSalt) =>
        new(Options.Create(new ImageProxyConfiguration
        {
            Endpoint = TestEndpoint,
            Key = key ?? string.Empty,
            Salt = salt ?? string.Empty,
            SourceUrlPrefix = TestPrefix,
        }));

    [Fact]
    public void BuildSquareThumbnail_EmptyObjectKey_ReturnsEmpty()
    {
        var sut = CreateBuilder();

        sut.BuildSquareThumbnail(string.Empty, 100).Should().BeEmpty();
    }

    [Fact]
    public void BuildSquareThumbnail_WithKey_ProducesSignedUrl()
    {
        var sut = CreateBuilder();

        var url = sut.BuildSquareThumbnail("avatars/abc.jpg", 400);

        // Structure: {endpoint}/{signature}/{options}/{base64url_source}
        url.Should().StartWith(TestEndpoint + "/");
        var rest = url[(TestEndpoint.Length + 1)..];
        var parts = rest.Split('/');
        parts.Should().HaveCountGreaterThanOrEqualTo(4);
        // signature — base64url, not "insecure"
        parts[0].Should().NotBe("insecure");
        parts[0].Should().NotBeEmpty();
        // options — holds resize:fill:400:400, gravity, quality
        var options = string.Join('/', parts[1..^1]);
        options.Should().Contain("rs:fill:400:400");
        options.Should().Contain("g:sm");
        options.Should().Contain("q:");
        // the last part — the base64url source URL
        parts[^1].Should().NotBeEmpty();
    }

    [Fact]
    public void BuildSquareThumbnail_WithoutKey_UsesInsecurePrefix()
    {
        // Dev mode: no key configured, imgproxy runs with IMGPROXY_ALLOW_INSECURE_URLS=true.
        var sut = CreateBuilder(key: string.Empty, salt: string.Empty);

        var url = sut.BuildSquareThumbnail("avatars/abc.jpg", 100);

        url.Should().StartWith(TestEndpoint + "/insecure/");
    }

    [Fact]
    public void BuildSquareThumbnail_DifferentSizes_ProduceDifferentUrls()
    {
        var sut = CreateBuilder();

        var small = sut.BuildSquareThumbnail("avatars/abc.jpg", 100);
        var medium = sut.BuildSquareThumbnail("avatars/abc.jpg", 400);

        small.Should().NotBe(medium);
        small.Should().Contain("rs:fill:100:100");
        medium.Should().Contain("rs:fill:400:400");
    }

    [Fact]
    public void BuildSquareThumbnail_SameInputs_AreDeterministic()
    {
        var sut = CreateBuilder();

        var a = sut.BuildSquareThumbnail("avatars/abc.jpg", 100);
        var b = sut.BuildSquareThumbnail("avatars/abc.jpg", 100);

        // Immutable signature for identical inputs — caching depends on it.
        a.Should().Be(b);
    }

    [Fact]
    public void BuildSquareThumbnail_DifferentObjectKey_ProduceDifferentSignatures()
    {
        var sut = CreateBuilder();

        var a = sut.BuildSquareThumbnail("avatars/abc.jpg", 100);
        var b = sut.BuildSquareThumbnail("avatars/xyz.png", 100);

        // The signature has to depend on the source URL — otherwise the source
        // can be swapped underneath it.
        a.Should().NotBe(b);
    }
}
