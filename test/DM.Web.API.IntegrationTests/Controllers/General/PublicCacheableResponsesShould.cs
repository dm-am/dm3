using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
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
/// The middleware keys by method and path, nothing sets `Vary`, and it runs above UseAuthentication. The
/// endpoints are found by their attribute, so tomorrow's policy is covered.
/// </remarks>
public class PublicCacheableResponsesShould : IntegrationTestBase
{
    public PublicCacheableResponsesShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    private sealed record PublicEndpoint(string Path, string Origin, int Duration);

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

    // `no-cache` keeps the middleware from answering out of its store, which would make leaked
    // bodies compare equal.
    private static HttpRequestMessage Uncached(HttpRequestMessage request)
    {
        request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true };
        return request;
    }

    // The wire values of X-Dm-Audience, taken from the parser rather than written out, so a
    // rendering audience added later is asked about too.
    private static IEnumerable<string> Audiences() =>
        Enum.GetValues<RenderAudience>().Select(BbAudienceHeader.Serialize);

    // X-Dm-Audience passes CORS and no endpoint sets Vary, as API_DESIGN.md requires the body to be
    // the same.
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

    // Through Uncached, so the header asserted is the one the endpoint produced.
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
