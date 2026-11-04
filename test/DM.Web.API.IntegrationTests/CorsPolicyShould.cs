using System.Net;
using AwesomeAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests;

/// <summary>
/// The CORS policy is two hand-kept lists, and both fail quietly: an unlisted request header is never sent, an
/// unexposed response header is unreadable to the page. The API has its own host, so the policy is driven directly.
/// </summary>
public class CorsPolicyShould : IntegrationTestBase
{
    /// <summary>An origin from SiteAddressConfiguration:AllowedOrigins that a browser really uses.</summary>
    private const string AllowedOrigin = "http://localhost:5173";

    public CorsPolicyShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Theory]
    [InlineData("content-type")]
    [InlineData("cache-control")]
    [InlineData("x-dm-audience")]
    [InlineData("x-dm-correlation-token")]
    [InlineData("idempotency-key")]
    // Token-gated actions read their credential from a header rather than the
    // path; unnamed here, the browser never sends the call at all.
    [InlineData("x-dm-account-token")]
    [InlineData("x-dm-ticket-token")]
    public async Task AllowEveryCustomRequestHeaderTheApiAccepts(string header)
    {
        var response = await SendPreflightAsync("/v1/uploads", "POST", header);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        // Header names are case-insensitive on the wire, and the policy answers with
        // the casing it was declared in.
        AllowedRequestHeaders(response).Should()
            .Contain(allowed => string.Equals(allowed, header, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RefuseAPreflightForAnUnlistedHeader()
    {
        // The policy is a list, not AllowAnyHeader: the unlisted name is absent from a non-empty answer.
        var response = await SendPreflightAsync("/v1/uploads", "POST", "x-dm-unlisted");

        var allowed = AllowedRequestHeaders(response);
        allowed.Should().NotBeEmpty("the policy names the headers it accepts");
        allowed.Should().NotContain(header =>
            string.Equals(header, "x-dm-unlisted", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AllowTheNegotiatePreflightOfTheHub()
    {
        // The SignalR client marks its negotiate call with both headers; refused, live events never connect.
        string[] requested = ["x-requested-with", "x-signalr-user-agent"];
        var response = await SendPreflightAsync(
            "/hubs/notifications/negotiate?negotiateVersion=1", "POST", string.Join(",", requested));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        CommaSeparated(response, "Access-Control-Allow-Origin").Should().Equal(AllowedOrigin);
        CommaSeparated(response, "Access-Control-Allow-Credentials").Should().Equal("true");
        var allowed = AllowedRequestHeaders(response);
        foreach (var header in requested)
        {
            allowed.Should().Contain(name => string.Equals(name, header, StringComparison.OrdinalIgnoreCase),
                "the client sends {0}", header);
        }
    }

    [Fact]
    public async Task ExposeTheResponseHeadersTheCallerHasToRead()
    {
        var request = CreateAuthenticatedRequest(HttpMethod.Get, "/v1/uploads");
        request.Headers.Add("Origin", AllowedOrigin);

        var response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var exposed = CommaSeparated(response, "Access-Control-Expose-Headers");
        exposed.Should().Contain("Location");
        exposed.Should().Contain("Retry-After");
    }

    private Task<HttpResponseMessage> SendPreflightAsync(string url, string method, string requestedHeader)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, url);
        request.Headers.Add("Origin", AllowedOrigin);
        request.Headers.Add("Access-Control-Request-Method", method);
        request.Headers.Add("Access-Control-Request-Headers", requestedHeader);
        return Client.SendAsync(request);
    }

    private static string[] AllowedRequestHeaders(HttpResponseMessage response) =>
        CommaSeparated(response, "Access-Control-Allow-Headers");

    /// <summary>
    /// A missing header reads as an empty list, so the failure is an assertion rather than an exception.
    /// </summary>
    private static string[] CommaSeparated(HttpResponseMessage response, string headerName) =>
        response.Headers.TryGetValues(headerName, out var values)
            ? values.SelectMany(value => value.Split(',')).Select(value => value.Trim()).ToArray()
            : [];
}
