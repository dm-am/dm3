using System.Net;
using System.Text.Json;
using DM.Web.API.Swagger;
using AwesomeAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.General;

/// <summary>
/// A list either takes a page or is bounded by what it is.
/// </summary>
/// <remarks>
/// A list that grows with the data needs a page; one bounded by its subject is named below. Asserted both ways,
/// so it only shrinks.
/// </remarks>
public class ListBoundsShould : IntegrationTestBase
{
    public ListBoundsShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    /// <summary>Any name under which a caller can ask for less than everything.</summary>
    private static readonly HashSet<string> PagingParameters =
        new(StringComparer.OrdinalIgnoreCase) { "skip", "take", "number", "size", "limit", "cursor" };

    /// <summary>
    /// Lists bounded by their subject, not by a page.
    /// </summary>
    /// <remarks>Adding a line is the decision "this cannot grow without bound".</remarks>
    private static readonly HashSet<string> BoundedBySubject = new(StringComparer.Ordinal)
    {
        "GET /v1/account/sessions",
        "GET /v1/achievement-categories",
        "GET /v1/attribute-schema-templates",
        "GET /v1/achievement-types",
        "GET /v1/award-types",
        "GET /v1/bans",
        "GET /v1/blogs/{id}/blacklist",
        "GET /v1/blogs/{id}/invitations",
        "GET /v1/blogs/{id}/readers",
        "GET /v1/blogs/{id}/users",
        "GET /v1/blogs/{id}/users/assistants",
        "GET /v1/boards",
        "GET /v1/boards/{id}/moderators",
        "GET /v1/contest-series",
        "GET /v1/games/tags",
        "GET /v1/games/{id}/blacklist",
        "GET /v1/games/{id}/chat-rooms",
        "GET /v1/games/{id}/invitations",
        "GET /v1/games/{id}/readers",
        "GET /v1/games/{id}/rooms",
        "GET /v1/games/{id}/users",
        "GET /v1/games/{id}/users/assistants",
        "GET /v1/global-chat/events",
        "GET /v1/moderation/achievement-categories",
        "GET /v1/moderation/achievement-types",
        "GET /v1/moderation/award-types",
        "GET /v1/moderation/contest-series",
        "GET /v1/moderation/contest-series/{id}/awards",
        "GET /v1/moderation/moderators",
        "GET /v1/moderation/tags",
        "GET /v1/moderation/tags/groups",
        "GET /v1/moderation/tags/groups/{groupId}/tags",
        "GET /v1/moderation/username-changes",
        "GET /v1/moderation/users/{username}/notes",
        "GET /v1/moderation/violators",
        "GET /v1/users/me/invitations",
        "GET /v1/users/me/subscriptions",
        "GET /v1/users/subscription-categories",
        "GET /v1/users/{username}/achievements",
        "GET /v1/users/{username}/awards",
    };

    /// <summary>
    /// Lists that grow with the data and have no page yet.
    /// </summary>
    /// <remarks>
    /// Their screens render the whole collection, so a page size is a visible change that waits for approval,
    /// and the server half alone would lose the tail.
    /// </remarks>
    private static readonly HashSet<string> GrowingWithoutAPage = new(StringComparer.Ordinal)
    {
        "GET /v1/blogs/{blogId}/notepad",
        "GET /v1/characters/{id}/master-notepad",
        "GET /v1/characters/{id}/notepad",
        "GET /v1/games/{id}/characters",
        "GET /v1/games/{id}/notepad",
        "GET /v1/users/me/notepad",
    };

    /// <summary>Every list this class accounts for, under either heading.</summary>
    private static IEnumerable<string> Accounted => BoundedBySubject.Concat(GrowingWithoutAPage);

    /// <summary>Published list GETs that declare no paging parameter.</summary>
    private async Task<HashSet<string>> UnpagedLists()
    {
        var unpaged = new HashSet<string>(StringComparer.Ordinal);

        foreach (var group in SwaggerExtensions.ApiGroups)
        {
            var response = await Client.GetAsync($"/swagger/{group}/swagger.json");
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            foreach (var path in document.RootElement.GetProperty("paths").EnumerateObject())
            {
                var shared = ParameterNames(path.Value);
                foreach (var operation in path.Value.EnumerateObject())
                {
                    if (operation.Name != "get" || operation.Value.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    if (!operation.Value.TryGetProperty("responses", out var responses) ||
                        !responses.TryGetProperty("200", out var success) ||
                        !AnswersAList(success))
                    {
                        continue;
                    }

                    var names = shared.Concat(ParameterNames(operation.Value));
                    if (!names.Any(PagingParameters.Contains))
                    {
                        unpaged.Add("GET " + path.Name);
                    }
                }
            }
        }

        return unpaged;
    }

    private static IEnumerable<string> ParameterNames(JsonElement node)
    {
        if (!node.TryGetProperty("parameters", out var parameters) ||
            parameters.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return parameters.EnumerateArray()
            .Where(p => p.TryGetProperty("name", out _))
            .Select(p => p.GetProperty("name").GetString()!)
            .ToArray();
    }

    /// <summary>Whether a 200 answers a list of resources.</summary>
    /// <remarks>A declared ListEnvelope, or an array written out in place.</remarks>
    private static bool AnswersAList(JsonElement success)
    {
        var schema = ResponseBodySchema.NameOf(success);
        if (schema == null)
        {
            return false;
        }

        return schema == ResponseBodySchema.WrittenInPlace
            ? ResponseBodySchema.IsArray(success)
            : schema.Contains("ListEnvelope", StringComparison.Ordinal);
    }

    [Fact]
    public async Task TakeAPageUnlessBoundedBySubject()
    {
        var unpaged = await UnpagedLists();
        unpaged.Should().NotBeEmpty("the API publishes list endpoints");

        unpaged.Should().BeSubsetOf(Accounted,
            "a list that grows with the data and takes no page has no upper bound on " +
            "its response, and the caller has no way to ask for less");
    }

    [Fact]
    public async Task LeaveNoStaleNameOnTheBoundedList()
    {
        var unpaged = await UnpagedLists();

        Accounted.Should().BeSubsetOf(unpaged,
            "an entry that outlives the endpoint it exempts is an exemption nobody " +
            "reviewed, waiting for a new list to be given the same address");
    }

    /// <summary>
    /// The two headings stay apart.
    /// </summary>
    /// <remarks>Moving a line from the growing list into the bounded one would make an open finding look closed.</remarks>
    [Fact]
    public void KeepTheDebtApartFromTheDecision() =>
        BoundedBySubject.Intersect(GrowingWithoutAPage).Should().BeEmpty(
            "a list is either bounded by what it is or waiting for a page, and the " +
            "difference is the whole content of both lists");

    /// <summary>
    /// A truncated list says how much it truncated.
    /// </summary>
    [Fact]
    public async Task ReportTheTotalOnATruncatedList()
    {
        var response = await Client.SendAsync(CreateAdminRequest(
            HttpMethod.Get, $"/v1/users/{TestConstants.TestUserLogin}/login-history?skip=0&take=1"));
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, "body was: {0}", body);

        var paging = JsonDocument.Parse(body).RootElement.GetProperty("paging");
        paging.GetProperty("take").GetInt32().Should().Be(1);
        paging.TryGetProperty("total", out _).Should().BeTrue(
            "a caller cannot tell a full page from a truncated one without the total");
    }
}
