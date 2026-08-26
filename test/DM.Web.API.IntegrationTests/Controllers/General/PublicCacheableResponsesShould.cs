using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DM.Infrastructure.Core.Parsing;
using AwesomeAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.General;

/// <summary>
/// A response the API declares publicly cacheable says the same thing to
/// everyone.
/// </summary>
/// <remarks>
/// `Cache-Control: public` on a cookie-authenticated API is a promise about
/// identity, and nothing was checking it. ResponseCachingMiddleware keys its
/// entries by method and path; `Vary` is set nowhere in this project, and the
/// middleware runs above UseAuthentication — so a personalised response stored
/// under a `public` policy is handed to the next caller whoever they are. The
/// only thing standing between that and a leak was the reading of five
/// controllers, done once.
///
/// The endpoints are found by their attribute rather than listed, so the check
/// covers the policy that will be written tomorrow. A new `[ResponseCache]`
/// with Location.Any on a list that reads the caller — the unread counters on
/// /v1/boards and /v1/games are the two that already carried one — fails here
/// instead of on the wire.
/// </remarks>
public class PublicCacheableResponsesShould : IntegrationTestBase
{
    public PublicCacheableResponsesShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    /// <summary>An action that publishes a shared-cache policy, and the path it answers.</summary>
    private sealed record PublicEndpoint(string Path, string Origin, int Duration);

    /// <summary>
    /// Every action declaring a response any cache may store and reuse.
    /// </summary>
    private static IReadOnlyCollection<PublicEndpoint> Declared()
    {
        var endpoints = new List<PublicEndpoint>();

        foreach (var controller in typeof(Startup).Assembly.GetTypes()
                     .Where(t => t is { IsAbstract: false, IsPublic: true } &&
                                 typeof(ControllerBase).IsAssignableFrom(t)))
        {
            var controllerRoute = controller.GetCustomAttributes<RouteAttribute>(inherit: true)
                .Select(a => a.Template).FirstOrDefault() ?? string.Empty;

            foreach (var action in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance |
                                                         BindingFlags.DeclaredOnly))
            {
                var policy = action.GetCustomAttribute<ResponseCacheAttribute>(inherit: true);
                if (policy is not { NoStore: false, Location: ResponseCacheLocation.Any })
                {
                    continue;
                }

                var http = action.GetCustomAttributes<HttpMethodAttribute>(inherit: true).FirstOrDefault();
                http.Should().NotBeNull($"{controller.Name}.{action.Name} declares a cache policy without a method");
                http!.HttpMethods.Should().Contain(HttpMethod.Get.Method,
                    $"{controller.Name}.{action.Name} caches a response, so it must be a GET");

                var template = http.Template ?? string.Empty;
                var path = template.StartsWith('~')
                    ? template.TrimStart('~', '/')
                    : string.Join('/', new[] { controllerRoute, template }.Where(s => !string.IsNullOrEmpty(s)));

                // A path segment or a required filter would make "the same bytes
                // for everyone" a claim about one argument rather than about the
                // endpoint. None of the catalogues take either, and a public
                // policy on one that did would need its own answer first.
                Regex.IsMatch(path, @"\{").Should().BeFalse(
                    $"{controller.Name}.{action.Name} is publicly cacheable and takes a path parameter");
                action.GetParameters().Should().BeEmpty(
                    $"{controller.Name}.{action.Name} is publicly cacheable and takes arguments");

                endpoints.Add(new PublicEndpoint($"/{path}", $"{controller.Name}.{action.Name}", policy.Duration));
            }
        }

        return endpoints;
    }

    /// <summary>
    /// Makes the request one ResponseCachingMiddleware answers from the endpoint
    /// rather than from its store.
    /// </summary>
    /// <remarks>
    /// Without it this test cannot see the fault it exists for. The entries are
    /// keyed by method and path, so the first call — the anonymous one — fills
    /// the store, and every later call on the same path is answered with that
    /// stored copy whatever identity it carries. The bodies then compare equal
    /// *because* the response leaked across callers, which is the one outcome
    /// this test must never read as success. `no-cache` on the request is the
    /// directive that forbids the middleware to serve from its store, and the
    /// question here is what the endpoint produces, not what the cache kept.
    /// </remarks>
    private static HttpRequestMessage Uncached(HttpRequestMessage request)
    {
        request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true };
        return request;
    }

    /// <summary>
    /// The wire values of <c>X-Dm-Audience</c>, taken from the parser rather than
    /// written out, so a rendering audience added later is asked about too.
    /// </summary>
    // The audiences a client may name, not every value of the enum: the
    // rendering intent behind a quotation is chosen by the endpoint that
    // composes one and has no wire value at all (see BbAudienceHeader.Wire).
    private static IEnumerable<string> Audiences() =>
        BbAudienceHeader.Wire.Select(BbAudienceHeader.Serialize);

    /// <summary>
    /// A publicly cacheable response says the same thing whatever rendering
    /// audience is asked for.
    /// </summary>
    /// <remarks>
    /// The second half of the rule in API_DESIGN.md, and the half nothing checked:
    /// "the same for anonymous, for any user AND for any value of X-Dm-Audience".
    /// The header is allowed through CORS, no endpoint sets <c>Vary</c>, and
    /// ResponseCachingMiddleware keys by method and path — so a body that renders
    /// its text differently per audience is stored under whichever audience asked
    /// first and handed to everyone else for the length of the policy. That is the
    /// same leak as the identity one, through a header instead of a cookie.
    /// </remarks>
    [Fact]
    public async Task BeTheSameBytesForEveryAudience()
    {
        var endpoints = Declared();
        endpoints.Should().NotBeEmpty(
            "the API declares shared-cache policies, and finding none would mean this test reads nothing");

        foreach (var endpoint in endpoints)
        {
            string? first = null;
            string? firstAudience = null;

            foreach (var audience in Audiences())
            {
                var request = Uncached(new HttpRequestMessage(HttpMethod.Get, endpoint.Path));
                request.Headers.Add(BbAudienceHeader.HeaderName, audience);

                var response = await Client.SendAsync(request);
                response.StatusCode.Should().Be(HttpStatusCode.OK,
                    $"{endpoint.Origin} is cached for everyone, so every audience must be answered");

                var body = await response.Content.ReadAsStringAsync();
                if (first == null)
                {
                    // Same reason as below: two empty catalogues agree by accident.
                    using var document = JsonDocument.Parse(body);
                    document.RootElement.GetProperty("resources").EnumerateArray().Should().NotBeEmpty(
                        $"{endpoint.Origin} must return rows for the comparison to mean anything");
                    first = body;
                    firstAudience = audience;
                    continue;
                }

                body.Should().Be(first,
                    $"{endpoint.Origin} answers Cache-Control: public with no Vary, so the copy stored " +
                    $"for '{firstAudience}' is handed to the caller who asked for '{audience}'");
            }
        }
    }

    [Fact]
    public async Task BeTheSameBytesForEveryCaller()
    {
        var endpoints = Declared();
        endpoints.Should().NotBeEmpty(
            "the API declares shared-cache policies, and finding none would mean this test reads nothing");

        foreach (var endpoint in endpoints)
        {
            var anonymous = await Client.SendAsync(Uncached(new HttpRequestMessage(HttpMethod.Get, endpoint.Path)));
            anonymous.StatusCode.Should().Be(HttpStatusCode.OK,
                $"{endpoint.Origin} is cached for everyone, so it must answer an anonymous caller");

            var asUser = await Client.SendAsync(
                Uncached(CreateAuthenticatedRequest(HttpMethod.Get, endpoint.Path)));
            var asAdmin = await Client.SendAsync(
                Uncached(CreateAdminRequest(HttpMethod.Get, endpoint.Path)));

            var anonymousBody = await anonymous.Content.ReadAsStringAsync();
            var userBody = await asUser.Content.ReadAsStringAsync();
            var adminBody = await asAdmin.Content.ReadAsStringAsync();

            // Two empty lists are equal whatever the endpoint does with the
            // caller, so an unseeded catalogue would make the comparison below
            // pass on any defect at all. It has to have rows to compare.
            using (var document = JsonDocument.Parse(anonymousBody))
            {
                document.RootElement.GetProperty("resources").EnumerateArray().Should().NotBeEmpty(
                    $"{endpoint.Origin} must return rows for the comparison to mean anything — " +
                    "an empty catalogue is identical for everyone by accident");
            }

            userBody.Should().Be(anonymousBody,
                $"{endpoint.Origin} answers Cache-Control: public, and a signed-in reader may be " +
                "served the copy stored for an anonymous one");
            adminBody.Should().Be(anonymousBody,
                $"{endpoint.Origin} answers Cache-Control: public, and an administrator may be " +
                "served the copy stored for anyone else — a moderation-only field in this body " +
                "would reach every reader for the next " + endpoint.Duration + " seconds");
        }
    }

    /// <summary>
    /// The policy the attribute declares is the one that reaches the wire.
    /// </summary>
    /// <remarks>
    /// A response filter, a middleware or a Set-Cookie on the way out can all
    /// turn a declared policy into a different header, and the difference is
    /// invisible until someone reads a capture.
    ///
    /// Through <see cref="Uncached" /> like the rest: a plain GET here can be
    /// answered from the store another test filled, and then the header asserted
    /// is the stored one rather than the one the endpoint just produced. Harmless
    /// while only headers are read, and exactly the trap the note on Uncached
    /// describes — so the request is made the same way everywhere.
    /// </remarks>
    [Fact]
    public async Task CarryThePolicyTheyDeclare()
    {
        foreach (var endpoint in Declared())
        {
            var response = await Client.SendAsync(
                Uncached(new HttpRequestMessage(HttpMethod.Get, endpoint.Path)));
            var cacheControl = response.Headers.CacheControl;

            cacheControl.Should().NotBeNull($"{endpoint.Origin} declares a cache policy");
            cacheControl!.Public.Should().BeTrue($"{endpoint.Origin} declares Location.Any");
            cacheControl.MaxAge.Should().Be(TimeSpan.FromSeconds(endpoint.Duration),
                $"{endpoint.Origin} declares Duration = {endpoint.Duration}");
        }
    }
}
