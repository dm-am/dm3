using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Testing;
using DM.Web.API.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace DM.Web.API.Tests.Middleware;

public class ErrorHandlingMiddlewareShould : UnitTestBase
{
    private const string InternalMessage =
        "42P01: relation \"Users\" does not exist; Host=db.internal;Password=secret";

    private readonly Guid _correlationId = Guid.NewGuid();

    [Fact]
    public async Task NotEchoUnhandledExceptionMessageToTheClient()
    {
        var response = await Handle(new InvalidOperationException(InternalMessage));

        response.RootElement.GetProperty("status").GetInt32().Should().Be(500);
        response.RootElement.GetProperty("title").GetString().Should().Be("Internal server error");
        response.RootElement.GetProperty("detail").GetString().Should().Contain(_correlationId.ToString());
        response.RootElement.ToString().Should().NotContain("42P01").And.NotContain("secret");
    }

    /// <summary>
    /// Messages of domain-authored exceptions are written for the caller and stay
    /// visible — the leak is specific to exceptions nobody wrote a message for.
    /// </summary>
    [Fact]
    public async Task KeepDomainAuthoredMessage()
    {
        var response = await Handle(new HttpException(System.Net.HttpStatusCode.Conflict, "Topic is closed"));

        response.RootElement.GetProperty("status").GetInt32().Should().Be(409);
        response.RootElement.GetProperty("title").GetString().Should().Be("Topic is closed");
    }

    private async Task<JsonDocument> Handle(Exception exception)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();

        var correlationTokenProvider = Mock<ICorrelationTokenProvider>();
        correlationTokenProvider.Setup(p => p.Current).Returns(_correlationId);

        var middleware = new ErrorHandlingMiddleware(_ => throw exception);
        await middleware.InvokeAsync(httpContext,
            NullLogger<ErrorHandlingMiddleware>.Instance,
            Mock<IIdentitySetter>().Object,
            correlationTokenProvider.Object,
            ProblemDetailsFactory);

        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        return await JsonDocument.ParseAsync(httpContext.Response.Body);
    }

    private static ProblemDetailsFactory ProblemDetailsFactory => new ServiceCollection()
        .AddLogging()
        .AddMvcCore().Services
        .BuildServiceProvider()
        .GetRequiredService<ProblemDetailsFactory>();
}
