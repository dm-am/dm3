using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using DM.Web.API.Swagger;
using FluentAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.General;

/// <summary>
/// A list either takes a page or is bounded by what it is.
/// </summary>
/// <remarks>
/// Forty-seven list endpoints declared no paging parameter of any kind. Most are
/// bounded by their subject — the tag catalogue, the boards, the moderators, the
/// assistants of one game — and those are fine and named below. The rest grew
/// with the data: every subscriber of an account, on a public endpoint, each one
/// a full user; the whole moderation intake queue on every open of the screen;
/// the login journal of an account, silently cut to fifty with no total and no
/// parameter, so an administrator reading an incident could not tell from the
/// answer that there was more of it.
///
/// The list is asserted in both directions, so it can only shrink. A new list
/// without paging turns the first test red, and an entry that has since grown a
/// paging parameter turns the second red — an exemption nobody has to maintain
/// outlives the reason it was granted.
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
    /// <remarks>
    /// The test is what makes the boundedness a claim somebody made rather than
    /// an omission. Adding a line here is the decision "this cannot grow without
    /// bound", and it is reviewable precisely because it has to be written down.
    /// </remarks>
    private static readonly HashSet<string> BoundedBySubject = new(StringComparer.Ordinal)
    {
        "GET /v1/account/sessions",
        "GET /v1/achievement-categories",
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
        "GET /v1/moderation/contest-series/{id}/awards",
        "GET /v1/moderation/moderators",
        "GET /v1/moderation/tags",
        "GET /v1/moderation/tags/groups",
        "GET /v1/moderation/tags/groups/{groupId}/tags",
        "GET /v1/moderation/tickets/assigned",
        "GET /v1/moderation/tickets/mine",
        "GET /v1/moderation/username-changes",
        "GET /v1/moderation/users/{username}/notes",
        "GET /v1/moderation/violators",
        "GET /v1/schemas",
        "GET /v1/users/me/invitations",
        "GET /v1/users/me/subscriptions",
        "GET /v1/users/{username}/achievements",
        "GET /v1/users/{username}/awards",
    };

    /// <summary>
    /// Lists that grow with the data and have no page yet.
    /// </summary>
    /// <remarks>
    /// Separated from the list above because the two claims are different and only
    /// one of them was true. The roster of a game and the five notepads were sitting
    /// among the endpoints declared bounded by their subject while the analysis of
    /// this finding said the opposite about them in as many words: CharacterRepository
    /// .GetCharacters and NotepadRepository both end in ToArrayAsync with no Skip and
    /// no Take, and a game accumulates characters (every application ever made, every
    /// retired character) and a notepad accumulates entries for as long as the game
    /// runs. Writing them down as "bounded by what they are" turned an open debt into
    /// a decision nobody made.
    ///
    /// Sitewide warnings joined them for the same reason and a plainer one: the
    /// repository reads every warning ever issued, ordered newest first, with no
    /// Skip and no Take, while the screen above it is titled "latest warnings".
    /// Nothing about a warning bounds how many there are.
    ///
    /// What closing them takes, and why it is not done here: paging a roster, a
    /// notepad or a moderation log is not only a query change. All of those screens
    /// render the whole collection today, so a page size takes rows off the screen —
    /// a visible change, and this project approves those one by one. The server half
    /// without the client half is worse than either: the roster would silently lose
    /// its tail.
    /// </remarks>
    private static readonly HashSet<string> GrowingWithoutAPage = new(StringComparer.Ordinal)
    {
        "GET /v1/blogs/{blogId}/notepad",
        "GET /v1/characters/{id}/notepad",
        "GET /v1/games/{id}/characters",
        "GET /v1/games/{id}/notepad",
        "GET /v1/moderation/warnings",
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
    /// <remarks>
    /// Two shapes, and only one of them used to count. A declared ListEnvelope is
    /// a class the document names; an array written out in place names nothing,
    /// and reading that as "no body" dropped the operation out of the walk before
    /// its parameters were ever looked at - so a list with no page and no
    /// envelope was on neither of the two lists above and held by nothing at all.
    /// </remarks>
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
    /// <remarks>
    /// Nothing else stops the debt from being quietly reclassified: moving a line
    /// from the growing list up into the bounded one is a one-word edit that makes
    /// an open finding look closed, which is how these five got there the first
    /// time.
    /// </remarks>
    [Fact]
    public void KeepTheDebtApartFromTheDecision() =>
        BoundedBySubject.Intersect(GrowingWithoutAPage).Should().BeEmpty(
            "a list is either bounded by what it is or waiting for a page, and the " +
            "difference is the whole content of both lists");

    /// <summary>
    /// A truncated list says how much it truncated.
    /// </summary>
    /// <remarks>
    /// The login journal is the case that made this worth asserting: it answered
    /// with fifty records and a null paging block, so the answer to "are there
    /// more" was not in the response at all.
    /// </remarks>
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
