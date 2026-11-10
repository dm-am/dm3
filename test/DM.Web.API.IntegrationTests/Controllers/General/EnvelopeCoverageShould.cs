using System.Text.Json;
using DM.Web.API.IntegrationTests.Helpers;
using AwesomeAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.General;

// A consumer cannot have one unwrapping function while some answers are bare.
public class EnvelopeCoverageShould : IntegrationTestBase
{
    public EnvelopeCoverageShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    private static readonly HashSet<string> Legacy = new(StringComparer.Ordinal)
    {
        "GET /v1/account/activation",
        "POST /v1/account/check-email",
        "POST /v1/account/check-username",
        "GET /v1/account/password-reset",
        "GET /v1/account/username-change",
        "GET /v1/account/username-change/approval",
        "GET /v1/chats/{id}",
        "GET /v1/endorsements/{id}",
        "GET /v1/moderation/notes/{id}",
        "GET /v1/moderation/tags/groups/{groupId}",
        "GET /v1/moderation/tags/{tagId}",
        "GET /v1/moderation/tickets/stats",
        "GET /v1/moderation/users/{username}/profile",
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

    private async Task<HashSet<string>> BareResponses()
    {
        var bare = new HashSet<string>(StringComparer.Ordinal);

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

                var schema = ResponseBodySchema.NameOf(status.Value);
                if (schema == null || schema.Contains("Envelope", StringComparison.Ordinal))
                {
                    continue;
                }

                bare.Add(operation.Address);
            }
        }

        return bare;
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
