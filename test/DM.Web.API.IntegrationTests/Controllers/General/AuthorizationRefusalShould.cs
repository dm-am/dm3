using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using DM.Domain.Core.Exceptions;
using FluentAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.General;

/// <summary>
/// The two refusals every endpoint can answer with look like every other refusal.
/// </summary>
/// <remarks>
/// Both were assembled by the authorization filters out of an anonymous object:
/// right status, right media type, no traceId — so neither could be matched to a
/// line in the log — and an English title. The title is what the client shows the
/// reader as it stands, and 401 and 403 are the two statuses no controller can
/// avoid, so that was English copy reachable from every screen of a Russian site.
///
/// Asserted over HTTP rather than on the filters: the property under test is what
/// leaves the host, and it is the middleware that decides it.
/// </remarks>
public class AuthorizationRefusalShould : IntegrationTestBase
{
    public AuthorizationRefusalShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task AnswerAnAnonymousCallerInTheCommonShapeAndInRussian()
    {
        var response = await Client.GetAsync("/v1/users/me/notepad");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("status").GetInt32().Should().Be(401);
        document.RootElement.GetProperty("title").GetString().Should().Be(
            RefusalMessage.AuthenticationRequired,
            "the client shows the title as it stands, so it is interface copy");
        document.RootElement.TryGetProperty("traceId", out _).Should()
            .BeTrue("a refusal without a correlation token cannot be found in the log");
        document.RootElement.TryGetProperty("type", out _).Should().BeTrue();
    }

    [Fact]
    public async Task AnswerTooLowARoleInTheCommonShapeAndInRussian()
    {
        // A regular user against a senior moderator's catalogue. The filter runs
        // before model binding, so the identifier is never looked up.
        var request = CreateAuthenticatedRequest(
            HttpMethod.Get, $"/v1/moderation/contest-series/{Guid.NewGuid()}/awards");

        var response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("status").GetInt32().Should().Be(403);
        document.RootElement.GetProperty("title").GetString().Should().Be(
            RefusalMessage.AccessDenied,
            "a role check and an intention refusal are one event to the reader");
        document.RootElement.TryGetProperty("traceId", out _).Should().BeTrue();
    }
}
