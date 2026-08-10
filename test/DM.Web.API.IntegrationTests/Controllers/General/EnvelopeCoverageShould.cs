using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using DM.Web.API.Swagger;
using FluentAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.General;

/// <summary>
/// Every success body is an envelope, and the ones that are not are counted.
/// </summary>
/// <remarks>
/// API_DESIGN calls Envelope the standard for a single resource and the bare DTO
/// legacy. Nothing held the code to it: the test next door pins four endpoints
/// out of 363, and 69 operations answer with a bare DTO. The cost is not
/// aesthetic — a consumer cannot have one unwrapping function, so apiClient.get
/// returns {resource: X} on one call and X on the next, and which is which lives
/// scattered through the call sites. That ambiguity has already rendered two
/// screens permanently empty.
///
/// Wrapping the remaining 69 in one pass is a migration, not a fix: the client is
/// hand-written and every one of them is a breaking change. What is fixed here is
/// the absence of a mechanism. The list below is the debt, named operation by
/// operation, and it is asserted in both directions: a new bare response turns
/// the first test red, an entry that no longer describes anything turns the
/// second red. It may only shrink.
/// </remarks>
public class EnvelopeCoverageShould : IntegrationTestBase
{
    public EnvelopeCoverageShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    /// <summary>Operations answering with a bare DTO, as inherited.</summary>
    private static readonly HashSet<string> Legacy = new(StringComparer.Ordinal)
    {
        "GET /v1/account/activation",
        "GET /v1/account/check-email",
        "GET /v1/account/check-username",
        "GET /v1/account/password-reset",
        "GET /v1/account/username-change",
        "GET /v1/account/username-change/approval",
        "GET /v1/chats/can-start/{username}",
        "GET /v1/chats/{id}",
        "GET /v1/endorsements/{id}",
        "GET /v1/moderation/notes/{id}",
        "GET /v1/moderation/tags/groups/{groupId}",
        "GET /v1/moderation/tags/{tagId}",
        "GET /v1/moderation/tickets/stats",
        "GET /v1/moderation/users/{username}/profile",
        "GET /v1/topics/{id}/discussion",
        "GET /v1/uploads/{id}",
        "GET /v1/users/me/blacklist/settings",
        "GET /v1/users/me/notes/{username}",
        "GET /v1/users/me/notifications/settings",
        "GET /v1/users/me/notifications/unread",
        "GET /v1/users/me/preferences",
        "GET /v1/users/me/profile",
        "GET /v1/users/me/subscriptions/check",
        "GET /v1/users/me/subscriptions/{id}",
        "GET /v1/users/{username}/bans",
        "GET /v1/users/{username}/subscribers/me",
        "GET /v1/users/{username}/warnings",
        "PATCH /v1/chats/{id}",
        "PATCH /v1/endorsements/{id}",
        "PATCH /v1/moderation/users/{username}/profile",
        "PATCH /v1/moderation/users/{username}/role/{role}",
        "PATCH /v1/users/me/blacklist/settings",
        "PATCH /v1/users/me/notifications/settings",
        "PATCH /v1/users/me/preferences",
        "PATCH /v1/users/me/profile",
        "PATCH /v1/users/me/subscriptions/{id}",
        "POST /v1/account/email-change",
        "POST /v1/account/login",
        "POST /v1/account/password",
        "POST /v1/account/password-reset",
        "POST /v1/account/recovery",
        "POST /v1/account/username-change",
        "POST /v1/account/username-change/complete",
        "POST /v1/blogs/comments/{id}/likes",
        "POST /v1/blogs/{id}/blacklist",
        "POST /v1/blogs/{id}/invitations/assistants",
        "POST /v1/blogs/{id}/invitations/readers",
        "POST /v1/blogs/{id}/readers",
        "POST /v1/chats",
        "POST /v1/chats/direct/{username}",
        "POST /v1/games/{id}/invitations/assistants",
        "POST /v1/games/{id}/invitations/players",
        "POST /v1/games/{id}/invitations/readers",
        "POST /v1/games/{id}/readers",
        "POST /v1/moderation/tags",
        "POST /v1/moderation/tags/groups",
        "POST /v1/moderation/users/{username}/notes",
        "POST /v1/tickets",
        "POST /v1/uploads",
        "POST /v1/users/me/blacklist",
        "POST /v1/users/me/notifications/bots/{type}",
        "POST /v1/users/me/subscriptions",
        "POST /v1/users/{username}/endorsements",
        "POST /v1/users/{username}/subscribers",
        "PUT /v1/moderation/notes/{id}",
        "PUT /v1/moderation/tags/groups/{groupId}",
        "PUT /v1/moderation/tags/{tagId}",
        "PUT /v1/users/me/notes/{username}",
    };

    /// <summary>Operations whose 2xx body is not an envelope, read off the document.</summary>
    private async Task<HashSet<string>> BareResponses()
    {
        var bare = new HashSet<string>(StringComparer.Ordinal);

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

                        var schema = SchemaNameOf(status.Value);
                        if (schema == null || schema.Contains("Envelope", StringComparison.Ordinal))
                        {
                            continue;
                        }

                        bare.Add(operation.Name.ToUpperInvariant() + " " + path.Name);
                    }
                }
            }
        }

        return bare;
    }

    /// <summary>The schema name a response body refers to, if it has a body.</summary>
    private static string? SchemaNameOf(JsonElement response)
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

    [Fact]
    public async Task WrapEverySuccessBodyOutsideTheLegacyList()
    {
        var bare = await BareResponses();
        bare.Should().NotBeEmpty("the document describes success bodies");

        bare.Should().BeSubsetOf(Legacy,
            "a consumer cannot have one unwrapping function while the shape varies " +
            "by endpoint, see the class remarks");
    }

    [Fact]
    public async Task LeaveNoStaleNameOnTheLegacyList()
    {
        var bare = await BareResponses();

        Legacy.Should().BeSubsetOf(bare,
            "an entry that outlives the endpoint it exempts is an exemption nobody " +
            "reviewed, waiting for a new endpoint to be given the same address");
    }
}
