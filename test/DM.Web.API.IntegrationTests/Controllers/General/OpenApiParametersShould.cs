using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using DM.Web.API.IntegrationTests.Helpers;
using DM.Web.API.Swagger;
using AwesomeAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.General;

public class OpenApiParametersShould : IntegrationTestBase
{
    public OpenApiParametersShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    // Repo-relative: the conventions document is what clients are written from, and it is not
    // copied to the output directory.
    private static readonly string ApiDesignPath =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "docs", "conventions", "API_DESIGN.md");

    /// <summary>Keys of a path item that are operations rather than metadata.</summary>
    private static readonly HashSet<string> HttpVerbs = new(StringComparer.Ordinal)
    {
        "get", "put", "post", "delete", "patch", "options", "head", "trace"
    };

    // An unbound query parameter is dropped in silence; an example URL can be diffed against the
    // wire.
    [Fact]
    public async Task DocumentOnlyExampleUrlsTheApiCanBind()
    {
        var declared = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        foreach (var group in SwaggerExtensions.ApiGroups)
        {
            var response = await Client.GetAsync($"/swagger/{group}/swagger.json");
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            foreach (var path in document.RootElement.GetProperty("paths").EnumerateObject())
            {
                // Parameters may sit on the path item and apply to every
                // operation under it, so both levels count as declared.
                var shared = QueryParameterNames(path.Value);

                foreach (var operation in path.Value.EnumerateObject())
                {
                    if (!HttpVerbs.Contains(operation.Name))
                    {
                        continue;
                    }

                    var key = operation.Name.ToUpperInvariant() + " " + path.Name;
                    if (!declared.TryGetValue(key, out var names))
                    {
                        names = new HashSet<string>(StringComparer.Ordinal);
                        declared[key] = names;
                    }

                    names.UnionWith(shared);
                    names.UnionWith(QueryParameterNames(operation.Value));
                }
            }
        }

        declared.Should().NotBeEmpty("the API publishes operations");

        File.Exists(ApiDesignPath).Should().BeTrue($"the conventions document must exist at {ApiDesignPath}");

        var wrong = new List<string>();
        var examples = 0;

        foreach (var line in await File.ReadAllLinesAsync(ApiDesignPath))
        {
            var match = Regex.Match(line.Trim(), @"^(GET|POST|PUT|PATCH|DELETE) (/[^\s?]+)\?(\S+)");
            if (!match.Success)
            {
                continue;
            }

            examples++;
            var key = match.Groups[1].Value + " " + match.Groups[2].Value;
            if (!declared.TryGetValue(key, out var names))
            {
                wrong.Add(key + " - no such operation");
                continue;
            }

            foreach (var pair in match.Groups[3].Value.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var name = pair.Split('=')[0];
                if (!names.Contains(name))
                {
                    wrong.Add(key + " - no query parameter " + name);
                }
            }
        }

        examples.Should().BeGreaterThan(0,
            "the document illustrates filtering and sorting with example URLs");
        wrong.Should().BeEmpty(
            "an example the API cannot bind is worse than none: the parameter is " +
            "dropped and the caller gets 200 in some other order");
    }

    private static HashSet<string> QueryParameterNames(JsonElement node)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        if (!node.TryGetProperty("parameters", out var parameters) ||
            parameters.ValueKind != JsonValueKind.Array)
        {
            return names;
        }

        foreach (var parameter in parameters.EnumerateArray())
        {
            if (parameter.TryGetProperty("in", out var location) &&
                location.GetString() == "query" &&
                parameter.TryGetProperty("name", out var name))
            {
                names.Add(name.GetString()!);
            }
        }

        return names;
    }

    /// <remarks>
    /// The enum is published by SortVocabularySwaggerFilter from the table SortVocabularyFilter enforces, so an
    /// endpoint without an entry arrives here as a bare string.
    /// </remarks>
    [Fact]
    public async Task DeclareTheSortFieldsOfEveryListEndpoint()
    {
        var undeclared = new List<string>();
        var declared = 0;

        foreach (var operation in await SwaggerDocuments.OperationsAsync(Client))
        {
            if (operation.Value.ValueKind != JsonValueKind.Object ||
                !operation.Value.TryGetProperty("parameters", out var parameters))
            {
                continue;
            }

            foreach (var parameter in parameters.EnumerateArray())
            {
                if (!parameter.TryGetProperty("name", out var name) ||
                    name.GetString() is not ("sortBy" or "sortOrder" or "sort"))
                {
                    continue;
                }

                if (DeclaresItsValues(parameter, operation.Document))
                {
                    declared++;
                }
                else
                {
                    undeclared.Add($"{operation.Address} {name.GetString()}");
                }
            }
        }

        declared.Should().BePositive("the API publishes sorted list endpoints");
        undeclared.Should().BeEmpty(
            "a sort parameter published as a bare string names no vocabulary, and the endpoint " +
            "behind it answers 200 to any value");
    }

    private static bool DeclaresItsValues(JsonElement parameter, JsonElement document)
    {
        if (!parameter.TryGetProperty("schema", out var schema))
        {
            return false;
        }

        if (schema.TryGetProperty("enum", out var inline) &&
            inline.ValueKind == JsonValueKind.Array &&
            inline.GetArrayLength() > 0)
        {
            return true;
        }

        if (!schema.TryGetProperty("$ref", out var reference))
        {
            return false;
        }

        var schemaName = reference.GetString()?.Split('/')[^1];
        if (schemaName == null ||
            !document.TryGetProperty("components", out var components) ||
            !components.TryGetProperty("schemas", out var schemas) ||
            !schemas.TryGetProperty(schemaName, out var referenced))
        {
            return false;
        }

        return referenced.TryGetProperty("enum", out var values) &&
               values.ValueKind == JsonValueKind.Array &&
               values.GetArrayLength() > 0;
    }

    /// <remarks>
    /// <c>?authors=alice,bob</c> on a collection binds one username "alice,bob". One string filter really splits on
    /// a comma, so the rule keys on the published type.
    /// </remarks>
    [Fact]
    public async Task NotDescribeARepeatableParameterAsCommaSeparated()
    {
        var wrong = new List<string>();
        var repeatableSeen = 0;

        foreach (var operation in await SwaggerDocuments.OperationsAsync(Client))
        {
            if (operation.Value.ValueKind != JsonValueKind.Object ||
                !operation.Value.TryGetProperty("parameters", out var parameters))
            {
                continue;
            }

            var repeatable = new List<string>();
            foreach (var parameter in parameters.EnumerateArray())
            {
                if (!parameter.TryGetProperty("schema", out var schema) ||
                    !schema.TryGetProperty("type", out var type) ||
                    type.GetString() != "array")
                {
                    continue;
                }

                var name = parameter.GetProperty("name").GetString()!;
                repeatable.Add(name);
                repeatableSeen++;

                if (parameter.TryGetProperty("description", out var own) &&
                    ClaimsCommaSeparated(own.GetString(), name))
                {
                    wrong.Add($"{operation.Address} parameter {name}");
                }
            }

            if (repeatable.Count == 0 ||
                !operation.Value.TryGetProperty("description", out var description))
            {
                continue;
            }

            foreach (var line in (description.GetString() ?? string.Empty).Split('\n'))
            {
                foreach (var name in repeatable)
                {
                    if (ClaimsCommaSeparated(line, name))
                    {
                        wrong.Add($"{operation.Address} description of {name}");
                    }
                }
            }
        }

        repeatableSeen.Should().BeGreaterThan(0, "the API publishes collection query parameters");
        wrong.Should().BeEmpty(
            "a collection parameter binds as a repeated key (authors=alice&authors=bob); " +
            "calling it comma-separated sends the caller down a path that answers 200 with an empty list");
    }

    private static bool ClaimsCommaSeparated(string? text, string parameterName) =>
        text != null &&
        text.Contains("comma-separated", StringComparison.OrdinalIgnoreCase) &&
        text.Contains(parameterName, StringComparison.OrdinalIgnoreCase);
}
