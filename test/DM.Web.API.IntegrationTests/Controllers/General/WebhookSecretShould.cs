using System.Net;
using System.Net.Http.Headers;
using System.Text;
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

    [Fact]
    public async Task RejectAWrongSecret()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/webhooks/telegram")
        {
            Content = EmptyUpdate(),
        };
        request.Headers.TryAddWithoutValidation("X-Telegram-Bot-Api-Secret-Token", "not-the-secret");

        var response = await Client.SendAsync(request);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.Forbidden);
        response.StatusCode.Should().NotBe(HttpStatusCode.OK);
    }
}
