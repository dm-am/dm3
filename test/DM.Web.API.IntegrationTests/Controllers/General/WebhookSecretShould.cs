using System.Collections.Generic;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using FluentAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.General;

/// <summary>
/// The bot webhook links an external chat id to a DM account, so its shared
/// secret is the only thing standing between an anonymous caller and someone
/// else's account. Two properties are load-bearing and neither is obvious from
/// reading the action: an unconfigured secret must close the endpoint rather
/// than open it, and the secret must never be accepted from the URL, because a
/// path is copied verbatim into every access log and trace.
/// </summary>
public class WebhookSecretShould : IntegrationTestBase
{
    public WebhookSecretShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    private static StringContent EmptyUpdate() =>
        new("{}", Encoding.UTF8, "application/json");

    [Theory]
    [InlineData("telegram")]
    [InlineData("discord")]
    public async Task RefuseAPathThatCarriesTheSecret(string type)
    {
        var response = await Client.PostAsync($"/v1/webhooks/{type}/whatever-secret", EmptyUpdate());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "the secret moved to a header; no route accepts it in the path");
    }

    [Theory]
    [InlineData("telegram")]
    [InlineData("discord")]
    public async Task RejectACallWithNoSecretHeader(string type)
    {
        var response = await Client.PostAsync($"/v1/webhooks/{type}", EmptyUpdate());

        // The fixture configures no bot secret, which is also the shipped
        // default: the endpoint must not exist rather than accept anything.
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private const string TelegramSecret = "telegram-webhook-secret-for-tests";
    private const string TelegramHeader = "X-Telegram-Bot-Api-Secret-Token";

    /// <summary>
    /// A host that has the secret configured, which the shared one deliberately
    /// has not: "no secret configured" is the shipped default and the tests above
    /// are about it. Without this the wrong-secret test returned 404 before the
    /// comparison, so FixedTimeEquals and the 403 arm were executed by nothing.
    /// </summary>
    private HttpClient ConfiguredClient() => DatabaseFixture.Factory
        .WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BotConfiguration:TelegramWebhookSecret"] = TelegramSecret
            })))
        .CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });

    private static HttpRequestMessage Update(string? secret)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/webhooks/telegram")
        {
            Content = EmptyUpdate()
        };
        if (secret != null)
        {
            request.Headers.TryAddWithoutValidation(TelegramHeader, secret);
        }

        return request;
    }

    [Fact]
    public async Task RejectAWrongSecretOnceOneIsConfigured()
    {
        using var client = ConfiguredClient();

        var response = await client.SendAsync(Update("not-the-secret"));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "the comparison is reached and refuses");
        // The refusal travels as the shape every other refusal in this host
        // travels as. This controller is hidden from the OpenAPI document, so the
        // ProblemDetails gate cannot see it, and it is where two hand-built
        // {"error": ...} bodies used to live.
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task RejectAMissingHeaderOnceASecretIsConfigured()
    {
        using var client = ConfiguredClient();

        var response = await client.SendAsync(Update(null));

        // An absent header is an empty string, which is not the secret: this is
        // the case the old "no secret header" test could never reach, because it
        // was answered by the missing configuration instead.
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AcceptTheConfiguredSecret()
    {
        using var client = ConfiguredClient();

        var response = await client.SendAsync(Update(TelegramSecret));

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "the right secret gets past the comparison; the payload itself is empty and the " +
            "handler answers 200 either way so the provider stops retrying");
    }
}
