using System.Net;
using System.Text.Json;
using DM.Web.API.Swagger;
using AwesomeAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.General;

/// <summary>
/// Every response the contract declares names the body the host writes: a
/// problem document under one media type for a refusal, JSON and nothing else
/// for a success.
/// </summary>
public class OpenApiResponsesShould : IntegrationTestBase
{
    public OpenApiResponsesShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    /// <summary>The media type a success body travels under.</summary>
    private const string JsonContentType = "application/json";

    /// <summary>
    /// The media type ErrorHandlingMiddleware writes on every refusal, repeated rather than imported.
    /// </summary>
    private const string ProblemJsonContentType = "application/problem+json";

    /// <summary>
    /// Every failure response is a problem document, and the contract says so.
    /// </summary>
    /// <remarks>ErrorHandlingMiddleware answers with ProblemDetails and application/problem+json; a new endpoint copies its neighbour's attributes.</remarks>
    [Fact]
    public async Task DescribeEveryFailureResponseAsAProblemDocument()
    {
        var wrong = new List<string>();
        var checked_ = 0;

        foreach (var group in SwaggerExtensions.ApiGroups)
        {
            var response = await Client.GetAsync($"/swagger/{group}/swagger.json");
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            foreach (var path in document.RootElement.GetProperty("paths").EnumerateObject())
            {
                foreach (var operation in path.Value.EnumerateObject())
                {
                    if (!operation.Value.TryGetProperty("responses", out var responses))
                    {
                        continue;
                    }

                    foreach (var status in responses.EnumerateObject())
                    {
                        if (!int.TryParse(status.Name, out var code) || code < 400)
                        {
                            continue;
                        }

                        checked_++;

                        // A failure response with no body is legitimate — 401 on
                        // an endpoint that says nothing beyond the status.
                        var schemaRef = SchemaReferenceOf(status.Value);
                        if (schemaRef == null)
                        {
                            continue;
                        }

                        if (!schemaRef.EndsWith("ProblemDetails", StringComparison.Ordinal))
                        {
                            wrong.Add($"{operation.Name.ToUpperInvariant()} {path.Name} {status.Name} -> {schemaRef}");
                        }

                        // The type is only half of the contract. A generated client
                        // picks its deserialiser by media type, and Swashbuckle's
                        // guess when no [Produces] is present names three of them —
                        // none the one the middleware writes.
                        var mediaTypes = MediaTypesOf(status.Value);
                        if (mediaTypes.Count != 1 || mediaTypes[0] != ProblemJsonContentType)
                        {
                            wrong.Add(
                                $"{operation.Name.ToUpperInvariant()} {path.Name} {status.Name} " +
                                $"-> {string.Join(", ", mediaTypes)}");
                        }
                    }
                }
            }
        }

        checked_.Should().BeGreaterThan(0, "the API declares failure responses");
        wrong.Should().BeEmpty(
            "the middleware answers every failure with ProblemDetails, so no endpoint may declare another type");
    }

    /// <summary>
    /// A success body is JSON, and the contract names that and nothing else.
    /// </summary>
    /// <remarks>One output formatter is registered, so text/plain and text/json are never written.</remarks>
    [Fact]
    public async Task DescribeEverySuccessBodyAsJson()
    {
        var wrong = new List<string>();
        var checked_ = 0;

        foreach (var group in SwaggerExtensions.ApiGroups)
        {
            var response = await Client.GetAsync($"/swagger/{group}/swagger.json");
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            foreach (var path in document.RootElement.GetProperty("paths").EnumerateObject())
            {
                foreach (var operation in path.Value.EnumerateObject())
                {
                    if (operation.Value.ValueKind != JsonValueKind.Object ||
                        !operation.Value.TryGetProperty("responses", out var responses))
                    {
                        continue;
                    }

                    foreach (var status in responses.EnumerateObject())
                    {
                        if (!int.TryParse(status.Name, out var code) || code >= 400)
                        {
                            continue;
                        }

                        // 204 and its neighbours declare no body at all, which is
                        // the honest description of an empty response.
                        var mediaTypes = MediaTypesOf(status.Value);
                        if (mediaTypes.Count == 0)
                        {
                            continue;
                        }

                        checked_++;
                        if (mediaTypes.Count != 1 || mediaTypes[0] != JsonContentType)
                        {
                            wrong.Add(
                                $"{operation.Name.ToUpperInvariant()} {path.Name} {status.Name} " +
                                $"-> {string.Join(", ", mediaTypes)}");
                        }
                    }
                }
            }
        }

        checked_.Should().BeGreaterThan(0, "the API declares response bodies");
        wrong.Should().BeEmpty(
            "the host has one output formatter, so a success body is application/json and nothing else");
    }

    /// <summary>Schema id an OpenAPI response body points at, if it declares one.</summary>
    private static string? SchemaReferenceOf(JsonElement response)
    {
        if (!response.TryGetProperty("content", out var content))
        {
            return null;
        }

        foreach (var mediaType in content.EnumerateObject())
        {
            if (mediaType.Value.TryGetProperty("schema", out var schema) &&
                schema.TryGetProperty("$ref", out var reference))
            {
                return reference.GetString();
            }
        }

        return null;
    }

    /// <summary>Media type names a response declares its body under.</summary>
    private static List<string> MediaTypesOf(JsonElement response)
    {
        var names = new List<string>();
        if (!response.TryGetProperty("content", out var content))
        {
            return names;
        }

        foreach (var mediaType in content.EnumerateObject())
        {
            names.Add(mediaType.Name);
        }

        return names;
    }
}
