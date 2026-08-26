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
/// <remarks>
/// 'unsafe-inline' in script-src is the directive being paid for, and the API
/// needs it for exactly one document: the Swagger page, which inlines its own
/// boot script and is mapped in Development alone. Everywhere else the API
/// answers JSON, and a document able to run an inline script from here is the
/// incident rather than the feature. The value is one string in one middleware,
/// which is precisely why widening it back costs one word and shows nowhere.
/// </remarks>
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

    /// <summary>
    /// connect-src names this origin, not every host that speaks a scheme.
    /// </summary>
    /// <remarks>
    /// A scheme source matches every host there is, so `wss:` here allowed a
    /// socket to anywhere at all and the directive restricted nothing. The
    /// single-origin topology is the whole of what is needed: 'self' covers
    /// ws:// and wss:// on the same host and port, which is where the hub is
    /// mapped, and the one document this origin ever serves is Swagger.
    /// </remarks>
    [Fact]
    public async Task NameNoHostBeyondThisOriginInConnectSrc()
    {
        var policy = await Policy(Environments.Production);

        policy.Should().Contain("connect-src 'self';");
        policy.Should().NotContain("wss:");
        policy.Should().NotContain(" ws:");
    }

    /// <summary>
    /// The retired header stays retired.
    /// </summary>
    /// <remarks>
    /// X-XSS-Protection asked for a filter that no browser this application runs
    /// in still has, and the one implementation that did have it was removed
    /// because the filter itself introduced holes. Sent anyway, it was a line in
    /// every response and a row in the requirements table claiming a defence that
    /// nothing performs - which is worse than no line, because a reader counts it.
    ///
    /// Asserted rather than deleted quietly: it is one line to add back, it breaks
    /// nothing when added, and the whole cost of it is that it says something
    /// untrue.
    /// </remarks>
    [Fact]
    public async Task SendNoHeaderThatNoBrowserStillHonours()
    {
        var headers = await Headers(Environments.Production);

        headers.Should().NotContainKey("X-XSS-Protection",
            "the filter it asks for exists in no browser this runs in, and asking for " +
            "it reads as a defence nothing performs");
    }

    private async Task<string> Policy(string environmentName) =>
        (await Headers(environmentName))["Content-Security-Policy"].ToString();

    private async Task<IHeaderDictionary> Headers(string environmentName)
    {
        var environment = Mock<IWebHostEnvironment>();
        environment.EnvironmentName.Returns(environmentName);

        var context = new DefaultHttpContext();
        var middleware = new SecurityHeadersMiddleware(_ => Task.CompletedTask, environment);

        await middleware.InvokeAsync(context);

        return context.Response.Headers;
    }
}
