using System.Net;
using System.Text.Json;
using DM.Web.API.Swagger;
using AwesomeAssertions;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.Game;

/// <remarks>
/// The page URL carries the five-letter public id. Checked against the registered route templates, where an
/// {id:guid} constraint lives; the published schema shows a plain string either way.
/// </remarks>
public class GameSubrouteIdentifierShould : IntegrationTestBase
{
    public GameSubrouteIdentifierShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public void AcceptThePublicIdOnEveryRegisteredGameSubroute()
    {
        var templates = DatabaseFixture.Factory.Services
            .GetRequiredService<IActionDescriptorCollectionProvider>()
            .ActionDescriptors.Items
            .Select(action => action.AttributeRouteInfo?.Template)
            .Where(template => template != null)
            .Select(template => template!)
            .Where(template => template.StartsWith("v1/games/{", StringComparison.Ordinal))
            .Distinct()
            .ToList();

        templates.Should().NotBeEmpty("the host registers the subroutes of a game");

        var constrained = templates
            .Where(template =>
            {
                var segment = template["v1/games/{".Length..];
                return segment[..segment.IndexOf('}')]
                    .Contains(":guid", StringComparison.OrdinalIgnoreCase);
            })
            .ToList();

        constrained.Should().BeEmpty(
            "the game segment resolves a public id on every other subroute, and a client " +
            "holding one off the page URL meets a bodiless routing 404 with nothing in the " +
            "contract to read it off");
    }

    // A guid-shaped game parameter in the document is the same defect written the other way round:
    // the action takes a Guid, so nothing but a Guid binds.
    [Fact]
    public async Task PublishNoGuidOnlyGameSegment()
    {
        var guidOnly = new List<string>();
        var examined = 0;

        foreach (var group in SwaggerExtensions.ApiGroups)
        {
            var response = await Client.GetAsync($"/swagger/{group}/swagger.json");
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            foreach (var path in document.RootElement.GetProperty("paths").EnumerateObject())
            {
                if (!path.Name.StartsWith("/v1/games/{", StringComparison.Ordinal))
                {
                    continue;
                }

                var segment = path.Name["/v1/games/{".Length..];
                var name = segment[..segment.IndexOf('}')];

                foreach (var operation in path.Value.EnumerateObject())
                {
                    if (operation.Value.ValueKind != JsonValueKind.Object ||
                        !operation.Value.TryGetProperty("parameters", out var parameters))
                    {
                        continue;
                    }

                    foreach (var parameter in parameters.EnumerateArray())
                    {
                        if (parameter.GetProperty("name").GetString() != name ||
                            !parameter.TryGetProperty("schema", out var schema))
                        {
                            continue;
                        }

                        examined++;
                        if (schema.TryGetProperty("format", out var format) &&
                            format.GetString() == "uuid")
                        {
                            guidOnly.Add($"{operation.Name.ToUpperInvariant()} {path.Name}");
                        }
                    }
                }
            }
        }

        examined.Should().BeGreaterThan(0, "the game paths declare their identifier parameter");
        guidOnly.Should().BeEmpty(
            "a client holding a public id off the page URL cannot tell which subroute refuses it");
    }

    [Fact]
    public async Task AnswerReviewsAddressedByPublicId()
    {
        var byPublicId = await Client.GetAsync($"/v1/games/{TestConstants.TestGamePublicId}/reviews");
        var body = await byPublicId.Content.ReadAsStringAsync();

        byPublicId.StatusCode.Should().Be(HttpStatusCode.OK, "body was: {0}", body);
        body.Should().Contain("resources");
    }

    [Fact]
    public async Task AnswerTheGameNotepadAddressedByPublicId()
    {
        using var request = CreateAuthenticatedRequest(
            HttpMethod.Get, $"/v1/games/{TestConstants.TestGamePublicId}/notepad");
        var response = await Client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().NotBe(HttpStatusCode.NotFound, "body was: {0}", body);
    }
}
