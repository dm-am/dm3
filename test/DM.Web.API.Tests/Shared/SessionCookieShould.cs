using System;
using System.Threading.Tasks;
using DM.Domain.Account.Configuration;
using DM.Domain.Core.Identity;
using DM.Testing;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Configuration;
using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using NSubstitute;
using Xunit;

namespace DM.Web.API.Tests.Shared;

// A Secure cookie over plain http is dropped silently; asserted on the Set-Cookie header a browser
// sees.
public class SessionCookieShould : UnitTestBase
{
    private static readonly ApiCredentialsStorage Storage =
        new(Options.Create(new AuthenticationConfiguration()),
            Options.Create(new SessionCookieConfiguration()));

    // Host and transport, deliberately crossed: a domain over http and localhost over https are the
    // two cases the host name answers backwards.
    public static TheoryData<string, bool> Transports => new()
    {
        { "dm.am", false },
        { "192.0.2.10", false },
        { "localhost", true }
    };

    [Theory]
    [MemberData(nameof(Transports))]
    public async Task MarkTheSessionSecureExactlyWhenTheRequestIsHttps(string host, bool isHttps)
    {
        var written = await Written(host, isHttps);

        written.Secure.Should().Be(isHttps,
            "a Secure cookie over http never reaches the cookie jar, and a plain one " +
            "over https travels in the clear");
    }

    // The origin check lets a request with neither Origin nor Referer through, and this attribute
    // is what keeps the session off it.
    [Fact]
    public async Task WithholdTheSessionFromCrossSiteSubrequests()
    {
        var written = await Written("dm.am", false);

        written.SameSite.Should().Be(Microsoft.Net.Http.Headers.SameSiteMode.Lax,
            "the origin check passes a request with neither Origin nor Referer, and what " +
            "keeps that from being a hole is the cookie not travelling cross-site");
    }

    [Theory]
    [MemberData(nameof(Transports))]
    public async Task ClearTheSessionWithTheAttributesItWasWrittenWith(string host, bool isHttps)
    {
        var written = await Written(host, isHttps);
        var cleared = await Cleared(host, isHttps);

        cleared.Secure.Should().Be(written.Secure,
            "a browser overwrites a cookie only when the attributes match, so a logout " +
            "that disagrees with the login leaves the session in place");
        cleared.Path.ToString().Should().Be(written.Path.ToString());
        cleared.SameSite.Should().Be(written.SameSite);
        cleared.HttpOnly.Should().Be(written.HttpOnly);
        cleared.Expires.Should().Be(DateTimeOffset.UnixEpoch, "logout has to expire the cookie");
    }

    private static async Task<SetCookieHeaderValue> Written(string host, bool isHttps)
    {
        var context = CreateContext(host, isHttps);
        await Storage.Load(context, AuthenticatedIdentity());
        return SetCookie(context);
    }

    private static async Task<SetCookieHeaderValue> Cleared(string host, bool isHttps)
    {
        var context = CreateContext(host, isHttps);
        await Storage.Unload(context);
        return SetCookie(context);
    }

    private static IIdentity AuthenticatedIdentity()
    {
        var identity = Mock<IIdentity>();
        identity.Session.Returns(new Session { Id = Guid.NewGuid() });
        identity.AuthenticationToken.Returns("token");
        return identity;
    }

    private static DefaultHttpContext CreateContext(string host, bool isHttps)
    {
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString(host);
        context.Request.IsHttps = isHttps;
        return context;
    }

    private static SetCookieHeaderValue SetCookie(HttpContext context) =>
        context.Response.GetTypedHeaders().SetCookie.Should().ContainSingle().Which;
}
