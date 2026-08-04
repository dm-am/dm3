using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using DM.Web.API.Swagger;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.Game;

/// <summary>
/// Every subroute of a game takes the same identifier as the game itself.
/// </summary>
/// <remarks>
/// A game is addressed by a five-letter public id everywhere the client has one,
/// and that is what the page URL carries. One subroute out of 33 declared
/// {id:guid} instead, so GET /v1/games/gamea/characters answered 200 while
/// GET /v1/games/gamea/reviews answered a bodiless routing 404 — no error body,
/// nothing in the contract to read it off, and a mandatory extra round trip
/// through GET /v1/games/{publicId} to obtain a GUID nothing else asks for.
///
/// Checked against the route templates the host registered rather than against a
/// list of paths, so a subroute added tomorrow with the constraint copied from a
/// neighbour is caught without anybody remembering this test exists.
///
/// The templates, and not the published schemas: the schema of a path parameter
/// comes from the type of the argument, so an action declaring {id:guid} while
/// taking a string publishes a plain string and refuses every public id at
/// routing time - the exact shape of this finding, invisible to a walk over the
/// document. The constraint is only in the template, which is where it is read.
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

    /// <summary>
    /// A guid-shaped game parameter in the document is the same defect written
    /// the other way round: the action takes a Guid, so nothing but a Guid binds.
    /// </summary>
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
