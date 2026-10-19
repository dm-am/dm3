using System.Net;
using System.Text.Json;
using DM.Web.API.Swagger;
using AwesomeAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.General;

/// <summary>
/// The API document is written out as a build artifact and asserted well-formed. A test rather than a CLI step:
/// this harness already has the encryption key, the XML doc file and the environment.
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
    /// Repo-relative and committed: a renamed server property shows as one line in review.
    /// </summary>
    private static readonly string ContractSnapshotPath =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "artifacts", "openapi-contract.json");

    /// <summary>
    /// Repo-relative and committed: every address the API answers.
    /// </summary>
    private static readonly string RoutesSnapshotPath =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "artifacts", "openapi-routes.json");

    /// <summary>
    /// The published addresses, as method and path.
    /// </summary>
    /// <remarks>The frontend suite holds every literal path it builds to this file.</remarks>
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
    /// Repo-relative, and committed. Every address the API answers without asking
    /// who is calling.
    /// </summary>
    private static readonly string PublicRoutesSnapshotPath =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "artifacts", "openapi-public-routes.json");

    /// <summary>
    /// Which operations are open to a visitor.
    /// </summary>
    /// <remarks>Dropping an authentication attribute becomes one added line in a file about what a stranger may reach.</remarks>
    [Fact]
    public async Task MatchTheCommittedPublicRoutesSnapshot()
    {
        var open = new SortedSet<string>(StringComparer.Ordinal);
        var total = 0;

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
                    total++;
                    if (!operation.Value.TryGetProperty("security", out _))
                    {
                        open.Add($"{operation.Name.ToUpperInvariant()} {path.Name}");
                    }
                }
            }
        }

        total.Should().BeGreaterThan(0, "the API publishes operations");
        open.Should().NotBeEmpty("the site is readable without an account");

        var serialised = JsonSerializer.Serialize(open, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }).Replace("\r\n", "\n") + "\n";

        var existing = File.Exists(PublicRoutesSnapshotPath)
            ? (await File.ReadAllTextAsync(PublicRoutesSnapshotPath)).Replace("\r\n", "\n")
            : null;

        if (existing == serialised)
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(PublicRoutesSnapshotPath)!);
        await File.WriteAllTextAsync(PublicRoutesSnapshotPath, serialised);

        existing.Should().NotBeNull(
            "artifacts/openapi-public-routes.json is missing; it has just been written, review and commit it");
        serialised.Should().Be(existing,
            "the set of operations open to a visitor changed; artifacts/openapi-public-routes.json has just " +
            "been rewritten, review the diff and commit it");
    }

    /// <summary>
    /// The published schemas, reduced to what a hand-written client has to
    /// agree with: schema name to its property names.
    /// </summary>
    /// <remarks>
    /// A snapshot: this test writes the contract and the frontend suite reads the committed file. Names only; the
    /// client declares its own types.
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

        // Utf8JsonWriter indents with the platform newline and .gitattributes stores LF, so both sides are LF.
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
}
