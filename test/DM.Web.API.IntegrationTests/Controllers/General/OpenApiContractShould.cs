using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DM.Web.API.Swagger;
using FluentAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.General;

/// <summary>
/// The API contract only ever existed inside a running Development container:
/// Swagger is not served in production and no build produced the document, so
/// nothing outside the process could be checked against it — which is how five
/// endpoints came to promise one response shape and put another on the wire.
///
/// This writes the document out as a build artifact and asserts it is
/// well-formed. It is a test rather than a CLI step on purpose: the CLI boots
/// the host itself and needs the encryption key, the XML doc file and the
/// environment supplied by hand, while this harness already has all three.
/// </summary>
public class OpenApiContractShould : IntegrationTestBase
{
    /// <summary>
    /// Repo-relative so CI can upload it. Cleaned and rewritten on every run —
    /// a stale contract is worse than none.
    /// </summary>
    private static readonly string OutputDirectory =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "artifacts", "openapi");

    public OpenApiContractShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task BeEmittedForEveryGroup()
    {
        var groups = SwaggerExtensions.ApiGroups;
        Directory.CreateDirectory(OutputDirectory);

        var written = 0;
        foreach (var group in groups)
        {
            var response = await Client.GetAsync($"/swagger/{group}/swagger.json");
            response.StatusCode.Should().Be(HttpStatusCode.OK, $"group {group} must have a document");

            var json = await response.Content.ReadAsStringAsync();

            // Well-formed and actually describing something: an empty paths
            // object would serialise fine and mean nothing.
            using var document = JsonDocument.Parse(json);
            document.RootElement.TryGetProperty("openapi", out _).Should().BeTrue();
            document.RootElement.GetProperty("paths").EnumerateObject().Should()
                .NotBeEmpty($"group {group} declares no paths");

            await File.WriteAllTextAsync(Path.Combine(OutputDirectory, $"{group}.json"), json);
            written++;
        }

        written.Should().BeGreaterThan(0, "the API publishes at least one OpenAPI group");
    }

    /// <summary>
    /// Repo-relative, and committed. Every response DTO the client mirrors by
    /// hand lives in it, so a property renamed on the server shows up as one
    /// line in review instead of as a field that silently reads undefined.
    /// </summary>
    private static readonly string ContractSnapshotPath =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "artifacts", "openapi-contract.json");

    /// <summary>
    /// Repo-relative, and committed. Every address the API answers, so a route
    /// the client asks for and the server does not serve is one line in review
    /// instead of a section that renders empty and says nothing.
    /// </summary>
    private static readonly string RoutesSnapshotPath =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "artifacts", "openapi-routes.json");

    /// <summary>
    /// The published addresses, as method and path.
    /// </summary>
    /// <remarks>
    /// The schema snapshot next to this one holds the shapes; nothing held the
    /// addresses, and the account security section spent its whole life asking
    /// GET /v1/account/security, which no controller has ever served. The
    /// frontend suite reads this file and holds every literal path it builds to
    /// it.
    /// </remarks>
    [Fact]
    public async Task MatchTheCommittedRoutesSnapshot()
    {
        var routes = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var group in SwaggerExtensions.ApiGroups)
        {
            var response = await Client.GetAsync($"/swagger/{group}/swagger.json");
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            if (!document.RootElement.TryGetProperty("paths", out var paths))
            {
                continue;
            }

            foreach (var path in paths.EnumerateObject())
            {
                foreach (var operation in path.Value.EnumerateObject())
                {
                    routes.Add($"{operation.Name.ToUpperInvariant()} {path.Name}");
                }
            }
        }

        routes.Should().NotBeEmpty("the API publishes routes");

        var serialised = JsonSerializer.Serialize(routes, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }).Replace("\r\n", "\n") + "\n";

        var existing = File.Exists(RoutesSnapshotPath)
            ? (await File.ReadAllTextAsync(RoutesSnapshotPath)).Replace("\r\n", "\n")
            : null;

        if (existing == serialised)
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(RoutesSnapshotPath)!);
        await File.WriteAllTextAsync(RoutesSnapshotPath, serialised);

        existing.Should().NotBeNull(
            "artifacts/openapi-routes.json is missing; it has just been written, review and commit it");
        serialised.Should().Be(existing,
            "the API routes changed; artifacts/openapi-routes.json has just been rewritten, review the diff and commit it");
    }

    /// <summary>
    /// The published schemas, reduced to what a hand-written client has to
    /// agree with: schema name to its property names.
    /// </summary>
    /// <remarks>
    /// A snapshot rather than a live comparison because the two sides run in
    /// different toolchains: this test writes the contract, and the frontend
    /// suite reads the committed file. Types are not compared — the client
    /// declares its own, and a name-level check already catches the drift that
    /// actually happened: a DTO wrapped in an envelope on one side only, and a
    /// field that existed in one model and not the other.
    /// </remarks>
    [Fact]
    public async Task MatchTheCommittedContractSnapshot()
    {
        var schemas = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);

        foreach (var group in SwaggerExtensions.ApiGroups)
        {
            var response = await Client.GetAsync($"/swagger/{group}/swagger.json");
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            if (!document.RootElement.TryGetProperty("components", out var components) ||
                !components.TryGetProperty("schemas", out var schemaNode))
            {
                continue;
            }

            foreach (var schema in schemaNode.EnumerateObject())
            {
                if (!schema.Value.TryGetProperty("properties", out var properties))
                {
                    continue;
                }

                if (!schemas.TryGetValue(schema.Name, out var names))
                {
                    names = new SortedSet<string>(StringComparer.Ordinal);
                    schemas[schema.Name] = names;
                }

                foreach (var property in properties.EnumerateObject())
                {
                    names.Add(property.Name);
                }
            }
        }

        schemas.Should().NotBeEmpty("the API publishes response schemas");

        // Utf8JsonWriter indents with the platform newline while .gitattributes
        // stores this file with LF, so on Windows a freshly cloned snapshot and
        // a freshly serialised one differ on every line — a failure the clean
        // filter then hides from the diff the message tells you to review.
        // Both sides are compared in LF, and LF is what gets written.
        var serialised = JsonSerializer.Serialize(schemas, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }).Replace("\r\n", "\n") + "\n";

        var existing = File.Exists(ContractSnapshotPath)
            ? (await File.ReadAllTextAsync(ContractSnapshotPath)).Replace("\r\n", "\n")
            : null;

        if (existing == serialised)
        {
            return;
        }

        // Rewrite before failing: the fix is to review the diff and commit it,
        // and having the new file on disk is what makes that possible.
        Directory.CreateDirectory(Path.GetDirectoryName(ContractSnapshotPath)!);
        await File.WriteAllTextAsync(ContractSnapshotPath, serialised);

        existing.Should().NotBeNull(
            "artifacts/openapi-contract.json is missing; it has just been written, review and commit it");
        serialised.Should().Be(existing,
            "the API contract changed; artifacts/openapi-contract.json has just been rewritten, review the diff and commit it");
    }

    /// <summary>The media type a success body travels under.</summary>
    private const string JsonContentType = "application/json";

    /// <summary>
    /// The media type ErrorHandlingMiddleware writes on every refusal. Repeated
    /// here rather than referenced: the point of the check is that the two sides
    /// agree, and importing the constant would make them agree by construction.
    /// </summary>
    private const string ProblemJsonContentType = "application/problem+json";

    /// <summary>
    /// Every failure response is a problem document, and the contract says so.
    /// </summary>
    /// <remarks>
    /// ErrorHandlingMiddleware answers with ProblemDetails and
    /// application/problem+json, and always has. The attributes named two DTOs
    /// that nothing ever wrote to the wire — 888 of the 890 declared failure
    /// responses — so every generated client got the wrong type for every error
    /// it could receive. Cleaning that up once is a tidy; this is what keeps it
    /// true, because a new endpoint copies the attributes of its neighbour.
    /// </remarks>
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
    /// <remarks>
    /// The same absent [Produces] put text/plain and text/json under every
    /// success body too. One output formatter is registered, so neither was ever
    /// written: a client generated from this document had two wrong deserialiser
    /// choices out of three for every response it reads.
    /// </remarks>
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

    /// <summary>
    /// Repo-relative: the conventions document is what clients are written from,
    /// and it is not copied to the output directory.
    /// </summary>
    private static readonly string ApiDesignPath =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "docs", "conventions", "API_DESIGN.md");

    /// <summary>Keys of a path item that are operations rather than metadata.</summary>
    private static readonly HashSet<string> HttpVerbs = new(StringComparer.Ordinal)
    {
        "get", "put", "post", "delete", "patch", "options", "head", "trace"
    };

    /// <summary>
    /// Every example URL in the conventions document names query parameters the
    /// endpoint actually declares.
    /// </summary>
    /// <remarks>
    /// The Sorting section was built on a composite parameter no endpoint has
    /// ever bound, three lines above a table using the real pair. A reader picks
    /// the nearer of two neighbouring sections, and an unbound query parameter
    /// is dropped in silence: the list comes back 200 in the default order, so
    /// "popular games" quietly means "newest". Prose cannot be diffed against
    /// the wire; an example URL can.
    /// </remarks>
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

    /// <summary>Query parameter names declared on a path item or an operation.</summary>
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

    /// <summary>
    /// Every sort parameter names the values it takes.
    /// </summary>
    /// <remarks>
    /// API_DESIGN says the allowed sort fields are declared beside the endpoint,
    /// in the parameter, and that an unknown one is a validation error rather
    /// than a silently different order. What was published instead was
    /// <c>"type": "string"</c> on thirteen sortBy parameters and fourteen
    /// sortOrder ones — a contract that names no vocabulary at all, over twelve
    /// endpoints that answered 200 to any value and sorted by their default.
    ///
    /// This test is the reason the vocabulary cannot be forgotten: the enum is
    /// published by SortVocabularySwaggerFilter out of the same table
    /// SortVocabularyFilter enforces, so an endpoint added without an entry
    /// there arrives here with a bare string and fails.
    /// </remarks>
    [Fact]
    public async Task DeclareTheSortFieldsOfEveryListEndpoint()
    {
        var undeclared = new List<string>();
        var declared = 0;

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

                        if (DeclaresItsValues(parameter, document.RootElement))
                        {
                            declared++;
                        }
                        else
                        {
                            undeclared.Add(
                                $"{operation.Name.ToUpperInvariant()} {path.Name} {name.GetString()}");
                        }
                    }
                }
            }
        }

        declared.Should().BeGreaterThan(20, "the API publishes sorted list endpoints");
        undeclared.Should().BeEmpty(
            "a sort parameter published as a bare string names no vocabulary, and the endpoint " +
            "behind it used to answer 200 to any value and sort by its default instead");
    }

    /// <summary>
    /// Whether a parameter's schema carries an enum, directly or through the
    /// $ref an enum type is published as.
    /// </summary>
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

    /// <summary>
    /// A parameter that repeats is not a parameter that splits on a comma, and
    /// the published description has to say which one it is.
    /// </summary>
    /// <remarks>
    /// Four comment listings documented their author filter as comma-separated.
    /// Nothing splits on a comma there: the property is a collection, so
    /// <c>?authors=alice,bob</c> binds as the single username "alice,bob", no
    /// author matches, and the endpoint answers 200 with an empty list — a wrong
    /// answer with no error to notice. One other filter really is
    /// comma-separated (a string, parsed by hand), which is why the rule keys on
    /// the published parameter type instead of banning the phrase.
    /// </remarks>
    [Fact]
    public async Task NotDescribeARepeatableParameterAsCommaSeparated()
    {
        var wrong = new List<string>();
        var repeatableSeen = 0;

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
                            wrong.Add($"{operation.Name.ToUpperInvariant()} {path.Name} parameter {name}");
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
                                wrong.Add($"{operation.Name.ToUpperInvariant()} {path.Name} description of {name}");
                            }
                        }
                    }
                }
            }
        }

        repeatableSeen.Should().BeGreaterThan(0, "the API publishes collection query parameters");
        wrong.Should().BeEmpty(
            "a collection parameter binds as a repeated key (authors=alice&authors=bob); " +
            "calling it comma-separated sends the caller down a path that answers 200 with an empty list");
    }

    /// <summary>Whether a line claims the named parameter takes a comma-separated value.</summary>
    private static bool ClaimsCommaSeparated(string? text, string parameterName) =>
        text != null &&
        text.Contains("comma-separated", StringComparison.OrdinalIgnoreCase) &&
        text.Contains(parameterName, StringComparison.OrdinalIgnoreCase);

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
