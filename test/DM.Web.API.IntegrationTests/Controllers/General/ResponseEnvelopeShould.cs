using System.Net;
using System.Text;
using System.Text.Json;
using DM.Web.API.Swagger;
using AwesomeAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.General;

/// <summary>
/// API_DESIGN.md declares the response shapes, and an endpoint's body matches its declared envelope.
/// </summary>
public class ResponseEnvelopeShould : IntegrationTestBase
{
    public ResponseEnvelopeShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    private const string ListEnvelopeSchema = "DM.Web.API.Shared.Dto.ListEnvelope`1";
    private const string EnvelopeSchema = "DM.Web.API.Shared.Dto.Envelope`1";

    /// <summary>
    /// Every GET that declares an envelope puts one on the wire.
    /// </summary>
    /// <remarks>
    /// The endpoints are enumerated from the published document and called; only argument-free operations, only a
    /// 200 examined, and a floor so that nothing checked is no pass.
    /// </remarks>
    [Fact]
    public async Task WrapEveryDeclaredEnvelopeOnTheWire()
    {
        var declared = new List<(string Url, string Property)>();

        foreach (var group in SwaggerExtensions.ApiGroups)
        {
            var document = await Client.GetAsync($"/swagger/{group}/swagger.json");
            document.StatusCode.Should().Be(HttpStatusCode.OK);

            using var json = JsonDocument.Parse(await document.Content.ReadAsStringAsync());
            foreach (var path in json.RootElement.GetProperty("paths").EnumerateObject())
            {
                var shared = RequiredParameterCount(path.Value);
                foreach (var operation in path.Value.EnumerateObject())
                {
                    if (operation.Name != "get" || operation.Value.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    if (shared + RequiredParameterCount(operation.Value) > 0 ||
                        !operation.Value.TryGetProperty("responses", out var responses) ||
                        !responses.TryGetProperty("200", out var success))
                    {
                        continue;
                    }

                    // A body the document writes out in place names neither envelope,
                    // so it declares nothing for the wire to be held to.
                    var schema = ResponseBodySchema.NameOf(success);
                    if (schema == null)
                    {
                        continue;
                    }

                    if (schema.StartsWith(ListEnvelopeSchema, StringComparison.Ordinal))
                    {
                        declared.Add((path.Name, "resources"));
                    }
                    else if (schema.StartsWith(EnvelopeSchema, StringComparison.Ordinal))
                    {
                        declared.Add((path.Name, "resource"));
                    }
                }
            }
        }

        declared.Should().HaveCountGreaterThan(30, "the API declares envelopes on argument-free listings");

        var wrong = new List<string>();
        var examined = 0;

        foreach (var (url, property) in declared)
        {
            var response = await Client.SendAsync(CreateAdminRequest(HttpMethod.Get, url));
            if (response.StatusCode != HttpStatusCode.OK)
            {
                continue;
            }

            examined++;
            var root = await ReadRoot(response);
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(property, out _))
            {
                wrong.Add($"GET {url} declares an envelope and answered without \"{property}\"");
            }
        }

        examined.Should().BeGreaterThan(20, "the probe has to reach the wire, not only the document");
        wrong.Should().BeEmpty(
            "the client believes the attribute: a bare array under a ListEnvelope declaration " +
            "renders an empty screen and fails nothing");
    }

    /// <summary>Required parameters declared on a path item or an operation.</summary>
    private static int RequiredParameterCount(JsonElement node)
    {
        if (!node.TryGetProperty("parameters", out var parameters) ||
            parameters.ValueKind != JsonValueKind.Array)
        {
            return 0;
        }

        return parameters.EnumerateArray().Count(parameter =>
            parameter.TryGetProperty("required", out var required) &&
            required.ValueKind == JsonValueKind.True);
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
