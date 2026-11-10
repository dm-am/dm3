using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using AwesomeAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.General;

// The webhook secret guards someone else's account: an unconfigured secret closes the endpoint, and
// the secret is never accepted from the URL, which every access log copies.
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

    // A host with the secret configured, which the shared one has not; it reaches the comparison
    // and the 403 arm.
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
        // The refusal has the shape of every other: this controller is hidden from the OpenAPI gate.
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
