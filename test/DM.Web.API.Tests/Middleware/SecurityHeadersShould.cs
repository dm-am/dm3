using System.Threading.Tasks;
using DM.Testing;
using DM.Web.API.Middleware;
using AwesomeAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Xunit;

namespace DM.Web.API.Tests.Middleware;

/// <summary>
/// This origin allows an inline script only where Swagger UI lives, and it names
/// the origin a page here may reach rather than whole schemes.
/// </summary>
/// <remarks>Swagger, mapped in Development alone, inlines its boot script; everywhere else the API answers JSON.</remarks>
public class SecurityHeadersShould : UnitTestBase
{
    [Fact]
    public async Task AllowNoInlineScriptOutsideDevelopment()
    {
        var policy = await Policy(Environments.Production);

        policy.Should().Contain("script-src 'self';");
        policy.Should().NotContain("script-src 'self' 'unsafe-inline'");
    }

    [Fact]
    public async Task AllowTheInlineScriptSwaggerCarriesInDevelopment()
    {
        var policy = await Policy(Environments.Development);

        policy.Should().Contain("script-src 'self' 'unsafe-inline'");
    }

    // A scheme source matches every host; 'self' covers ws:// and wss:// where the hub is mapped.
    [Fact]
    public async Task NameNoHostBeyondThisOriginInConnectSrc()
    {
        var policy = await Policy(Environments.Production);

        policy.Should().Contain("connect-src 'self';");
        policy.Should().NotContain("wss:");
        policy.Should().NotContain(" ws:");
    }

    // X-XSS-Protection asks for a filter no browser has; sent, it claims a defence nothing
    // performs.
    [Fact]
    public async Task SendNoHeaderThatNoBrowserStillHonours()
    {
        var headers = await Headers(Environments.Production);

        headers.Should().NotContainKey("X-XSS-Protection",
            "the filter it asks for exists in no browser this runs in, and asking for " +
            "it reads as a defence nothing performs");
    }

    private static async Task<string> Policy(string environmentName) =>
        (await Headers(environmentName))["Content-Security-Policy"].ToString();

    private static async Task<IHeaderDictionary> Headers(string environmentName)
    {
        var environment = Mock<IWebHostEnvironment>();
        environment.EnvironmentName.Returns(environmentName);

        var context = new DefaultHttpContext();
        var middleware = new SecurityHeadersMiddleware(_ => Task.CompletedTask, environment);

        await middleware.InvokeAsync(context);

        return context.Response.Headers;
    }
}
