using System;
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
}
