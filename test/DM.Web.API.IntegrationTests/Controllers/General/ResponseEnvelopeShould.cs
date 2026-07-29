using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.General;

/// <summary>
/// API_DESIGN.md declares the response shapes, and nothing held the code to them:
/// five list endpoints promised ListEnvelope in their OpenAPI attribute and put a
/// bare array on the wire, and the hand-written client believed the attribute.
/// Two screens rendered permanently empty because of it — the notification list
/// and every subscription list — with no test failing anywhere.
/// </summary>
public class ResponseEnvelopeShould : IntegrationTestBase
{
    public ResponseEnvelopeShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Theory]
    [InlineData("/v1/users/me/notifications")]
    [InlineData("/v1/users/me/subscriptions")]
    [InlineData("/v1/users/me/notepad")]
    public async Task WrapAListInResources(string url)
    {
        var response = await Client.SendAsync(CreateAuthenticatedRequest(HttpMethod.Get, url));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var root = await ReadRoot(response);
        root.ValueKind.Should().Be(JsonValueKind.Object, "a list response is an envelope, not a bare array");
        root.TryGetProperty("resources", out var resources).Should().BeTrue();
        resources.ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task WrapASingleResourceInResource()
    {
        var create = CreateAuthenticatedRequest(HttpMethod.Post, "/v1/users/me/notepad");
        create.Content = new StringContent(
            """{"title":"Проверка конверта","content":"тело"}""", Encoding.UTF8, "application/json");

        var created = await Client.SendAsync(create);
        created.StatusCode.Should().Be(HttpStatusCode.Created);

        var root = await ReadRoot(created);
        root.TryGetProperty("resource", out var resource).Should().BeTrue();
        var id = resource.GetProperty("id").GetString();
        id.Should().NotBeNullOrEmpty();

        try
        {
            // The update is what actually broke a screen: the client assigned the
            // whole envelope into the list row, so the row lost its id and title.
            var update = CreateAuthenticatedRequest(HttpMethod.Patch, $"/v1/users/me/notepad/{id}");
            update.Content = new StringContent(
                """{"title":"Правка","content":"тело"}""", Encoding.UTF8, "application/json");

            var updated = await Client.SendAsync(update);
            updated.StatusCode.Should().Be(HttpStatusCode.OK);

            var updatedRoot = await ReadRoot(updated);
            updatedRoot.TryGetProperty("resource", out var updatedResource).Should().BeTrue();
            updatedResource.GetProperty("title").GetString().Should().Be("Правка");
        }
        finally
        {
            await Client.SendAsync(CreateAuthenticatedRequest(
                HttpMethod.Delete, $"/v1/users/me/notepad/{id}"));
        }
    }

    private static async Task<JsonElement> ReadRoot(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(body).RootElement.Clone();
    }
}
