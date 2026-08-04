using System.Threading.Tasks;
using DM.Testing;
using DM.Web.API.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;

namespace DM.Web.API.Tests.Middleware;

/// <summary>
/// This origin allows an inline script only where Swagger UI lives.
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

    private async Task<string> Policy(string environmentName)
    {
        var environment = Mock<IWebHostEnvironment>();
        environment.Setup(e => e.EnvironmentName).Returns(environmentName);

        var context = new DefaultHttpContext();
        var middleware = new SecurityHeadersMiddleware(_ => Task.CompletedTask, environment.Object);

        await middleware.InvokeAsync(context);

        return context.Response.Headers["Content-Security-Policy"].ToString();
    }
}
