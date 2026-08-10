using System;
using System.Net;
using FluentAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests;

/// <summary>
/// The CORS policy is two hand kept lists, and both of them fail quietly.
///
/// A request header missing from the allow list is never sent at all: the
/// preflight comes back without it and the browser cancels the request before it
/// leaves. That is what the client's Idempotency-Key hit, attached to every file
/// upload while the policy listed seven other headers and not that one, with the
/// checked in dev setup already cross origin: the SPA on 5173, the API on 5000,
/// no Vite proxy between them. A response header missing from the exposed list
/// does arrive and stays unreadable to the page, which covers Location on 201
/// and Retry-After on 429, the one the client's 429 handler reads.
///
/// Neither fault shows up where a reverse proxy serves the SPA and the API from
/// one origin, because then there is no preflight and no cross origin response
/// to hide anything from. The published contract puts the API on its own host,
/// so these tests drive the policy directly instead of waiting for the topology
/// to catch up with the document.
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
    [InlineData("x-requested-with")]
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
        // The other half of the theory above: the policy is a list, not
        // AllowAnyHeader. Without this, allowing everything would satisfy every
        // case above while handing each listed origin a blank cheque.
        //
        // The answer is the list itself, not a refusal: the middleware replies
        // with what it allows and leaves the comparison to the browser, which
        // will not send the call. So the check is that the unlisted name is not
        // in the answer, over a list that is not empty either.
        var response = await SendPreflightAsync("/v1/uploads", "POST", "x-dm-unlisted");

        var allowed = AllowedRequestHeaders(response);
        allowed.Should().NotBeEmpty("the policy names the headers it accepts");
        allowed.Should().NotContain(header =>
            string.Equals(header, "x-dm-unlisted", StringComparison.OrdinalIgnoreCase));
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
    /// A missing header reads as an empty list rather than throwing: an absent
    /// allow list is exactly the failure under test, and it has to fail as an
    /// assertion, not as an exception from the reading helper.
    /// </summary>
    private static string[] CommaSeparated(HttpResponseMessage response, string headerName) =>
        response.Headers.TryGetValues(headerName, out var values)
            ? values.SelectMany(value => value.Split(',')).Select(value => value.Trim()).ToArray()
            : [];
}
