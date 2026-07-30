using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.Json;
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
                    }
                }
            }
        }

        checked_.Should().BeGreaterThan(0, "the API declares failure responses");
        wrong.Should().BeEmpty(
            "the middleware answers every failure with ProblemDetails, so no endpoint may declare another type");
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
}
