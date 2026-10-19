using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Testing;
using DM.Testing.Dsl;
using System.Collections.Generic;
using System.Net;
using FluentValidation;
using DM.Web.API.Middleware;
using AwesomeAssertions;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
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
        response.RootElement.GetProperty("title").GetString().Should().Be("Ошибка сервера");
        response.RootElement.GetProperty("detail").GetString().Should().NotContain("Ошибка сервера");
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

    /// <summary>
    /// The interface is Russian, and several call sites read the title of a 400 straight out of the problem document.
    /// </summary>
    [Fact]
    public async Task TitleAValidationFailureInRussian()
    {
        var response = await Handle(
            new ValidationException(new[] { new ValidationFailure("Name", "Empty") }));

        response.RootElement.GetProperty("status").GetInt32().Should().Be(400);
        response.RootElement.GetProperty("title").GetString().Should().Be("Некорректные данные");
    }

    /// <summary>
    /// A refusal that names its reason only in general carries the mark the
    /// client tells it by: its words are the same for every call it refuses.
    /// </summary>
    [Fact]
    public async Task MarkTheUnhandledFailureGeneral()
    {
        var response = await Handle(new InvalidOperationException(InternalMessage));

        response.RootElement.GetProperty("general").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task MarkTheIntentionRefusalGeneral()
    {
        var response = await Handle(new IntentionManagerException(
            Create.User().Please(), DayOfWeek.Monday));

        response.RootElement.GetProperty("status").GetInt32().Should().Be(403);
        response.RootElement.GetProperty("title").GetString().Should().Be(RefusalMessage.AccessDenied);
        response.RootElement.GetProperty("general").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task MarkAHandWrittenAccessRefusalGeneral()
    {
        var response = await Handle(new HttpException(HttpStatusCode.Forbidden, RefusalMessage.AccessDenied));

        response.RootElement.GetProperty("general").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task LeaveARefusalThatNamesItsReasonUnmarked()
    {
        var response = await Handle(new HttpException(HttpStatusCode.Forbidden, "Комментарии в топике закрыты"));

        response.RootElement.TryGetProperty("general", out _).Should().BeFalse();
    }

    /// <summary>
    /// A refusal of fields written by hand is titled by its message, as one from
    /// the validator is: without it the framework titled the document in English.
    /// </summary>
    [Fact]
    public async Task TitleAHandWrittenFieldRefusalByItsMessage()
    {
        var response = await Handle(new HttpBadRequestException(
            new Dictionary<string, string> { ["month"] = "Месяц должен быть от 1 до 12" }));

        response.RootElement.GetProperty("status").GetInt32().Should().Be(400);
        response.RootElement.GetProperty("title").GetString().Should().Be(RefusalMessage.InvalidData);
    }

    private async Task<JsonDocument> Handle(Exception exception)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();

        var correlationTokenProvider = Mock<ICorrelationTokenProvider>();
        correlationTokenProvider.Current.Returns(_correlationId);

        var middleware = new ErrorHandlingMiddleware(_ => throw exception);
        await middleware.InvokeAsync(httpContext,
            NullLogger<ErrorHandlingMiddleware>.Instance,
            Mock<IIdentityProvider>(),
            correlationTokenProvider,
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
