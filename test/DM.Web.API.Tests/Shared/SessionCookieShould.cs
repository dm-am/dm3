using System;
using System.Threading.Tasks;
using DM.Domain.Account.Configuration;
using DM.Domain.Core.Identity;
using DM.Testing;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Configuration;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using Xunit;

namespace DM.Web.API.Tests.Shared;

/// <summary>
/// The transport decides the Secure attribute of the session cookie.
/// </summary>
/// <remarks>
/// Both halves of the failure are silent: a Secure cookie handed out over plain
/// http is dropped by the browser without an error, so the login answers 200 and
/// the next request arrives anonymous. That is what the host name produced on
/// every stand not called localhost. Asserted on the Set-Cookie header rather
/// than on CookieOptions, because the header is the whole of what a browser sees.
/// </remarks>
public class SessionCookieShould : UnitTestBase
{
    private static readonly ApiCredentialsStorage Storage =
        new(Options.Create(new AuthenticationConfiguration()),
            Options.Create(new SessionCookieConfiguration()));

    /// <summary>
    /// Host and transport, deliberately crossed: a domain over http and localhost
    /// over https are the two cases the host name answers backwards.
    /// </summary>
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

    private async Task<SetCookieHeaderValue> Written(string host, bool isHttps)
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

    private IIdentity AuthenticatedIdentity()
    {
        var identity = Mock<IIdentity>();
        identity.SetupGet(i => i.Session).Returns(new Session { Id = Guid.NewGuid() });
        identity.SetupGet(i => i.AuthenticationToken).Returns("token");
        return identity.Object;
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
