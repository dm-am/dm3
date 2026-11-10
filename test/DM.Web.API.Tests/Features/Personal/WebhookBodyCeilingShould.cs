using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Identity;
using DM.Testing;
using DM.Web.API.Features.Personal.Webhooks;
using DM.Web.API.Middleware;
using AwesomeAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace DM.Web.API.Tests.Features.Personal;

/// <remarks>
/// On a listening Kestrel: the ceiling is the server's own refusal to read on, and neither a bare
/// controller nor the in-memory server of the integration tests makes it.
/// </remarks>
public class WebhookBodyCeilingShould : UnitTestBase, IAsyncLifetime
{
    private WebApplication _host = null!;
    private HttpClient _client = null!;

    public async ValueTask InitializeAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Services
            .AddSingleton(Mock<IIdentityProvider>())
            .AddSingleton(Mock<ICorrelationTokenProvider>())
            .AddSingleton(new DiscordSignatureVerifier(Mock<IDateTimeProvider>(), Mock<ILogger<DiscordSignatureVerifier>>()))
            .AddControllers()
            .ConfigureApplicationPartManager(parts =>
            {
                parts.ApplicationParts.Clear();
                parts.ApplicationParts.Add(new WebhookPart());
            });

        _host = builder.Build();
        _host.UseMiddleware<ErrorHandlingMiddleware>();
        _host.MapControllers();
        await _host.StartAsync();

        _client = new HttpClient { BaseAddress = new Uri(_host.Urls.Single()) };
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _host.DisposeAsync();
    }

    // No bot is configured and the type is nobody's: the worst caller, with no credential to fail on.
    private Task<HttpResponseMessage> PostBytes(int count) =>
        _client.PostAsync("/v1/webhooks/slack", new ByteArrayContent(new byte[count]), TestContext.Current.CancellationToken);

    [Fact]
    public async Task RefuseABodyOverTheCeilingAsARefusalOfItsOwn()
    {
        using var response = await PostBytes(WebhookController.MaxBodyBytes + 1);

        response.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge,
            "the server's refusal is not an unhandled failure of ours");
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task RefuseAnOversizedBodyThatDoesNotDeclareItsLength()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/webhooks/slack")
        {
            Content = new ByteArrayContent(new byte[WebhookController.MaxBodyBytes + 1]),
        };
        request.Headers.TransferEncodingChunked = true;

        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
    }

    [Fact]
    public async Task ReadABodyAtTheCeilingThrough()
    {
        using var response = await PostBytes(WebhookController.MaxBodyBytes);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound, "the body passed and the type decided");
    }

    private sealed class WebhookPart : ApplicationPart, IApplicationPartTypeProvider
    {
        public override string Name => nameof(WebhookController);

        public IEnumerable<TypeInfo> Types => [typeof(WebhookController).GetTypeInfo()];
    }
}
