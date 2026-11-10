using System.Text.Json;
using DM.Web.API.IntegrationTests.Helpers;
using AwesomeAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.General;

public class OpenApiResponsesShould : IntegrationTestBase
{
    public OpenApiResponsesShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    private const string JsonContentType = "application/json";

    // The media type ErrorHandlingMiddleware writes on every refusal, repeated rather than
    // imported.
    private const string ProblemJsonContentType = "application/problem+json";

    // ErrorHandlingMiddleware answers with ProblemDetails and application/problem+json; a new
    // endpoint copies its neighbour's attributes.
    [Fact]
    public async Task DescribeEveryFailureResponseAsAProblemDocument()
    {
        var wrong = new List<string>();
        var checked_ = 0;

        foreach (var operation in await SwaggerDocuments.OperationsAsync(Client))
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
                    wrong.Add($"{operation.Address} {status.Name} -> {schemaRef}");
                }

                // The type is only half of the contract. A generated client
                // picks its deserialiser by media type, and Swashbuckle's
                // guess when no [Produces] is present names three of them —
                // none the one the middleware writes.
                var mediaTypes = MediaTypesOf(status.Value);
                if (mediaTypes.Count != 1 || mediaTypes[0] != ProblemJsonContentType)
                {
                    wrong.Add(
                        $"{operation.Address} {status.Name} " +
                        $"-> {string.Join(", ", mediaTypes)}");
                }
            }
        }

        checked_.Should().BeGreaterThan(0, "the API declares failure responses");
        wrong.Should().BeEmpty(
            "the middleware answers every failure with ProblemDetails, so no endpoint may declare another type");
    }

    // One output formatter is registered, so text/plain and text/json are never written.
    [Fact]
    public async Task DescribeEverySuccessBodyAsJson()
    {
        var wrong = new List<string>();
        var checked_ = 0;

        foreach (var operation in await SwaggerDocuments.OperationsAsync(Client))
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
                        $"{operation.Address} {status.Name} " +
                        $"-> {string.Join(", ", mediaTypes)}");
                }
            }
        }

        checked_.Should().BeGreaterThan(0, "the API declares response bodies");
        wrong.Should().BeEmpty(
            "the host has one output formatter, so a success body is application/json and nothing else");
    }

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
