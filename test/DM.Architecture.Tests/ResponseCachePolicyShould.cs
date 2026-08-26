using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// The cache policy the server declares is a policy the only consumer can obey.
/// </summary>
/// <remarks>
/// The API registers response caching and declares `public, max-age=300` on five
/// catalogues, and the number of hits either cache ever served was zero: the
/// client put `Cache-Control: no-cache` on the default headers of every request
/// it makes. On the request side that directive forbids
/// ResponseCachingMiddleware from reading its store at all, and requires the
/// browser to revalidate before reusing anything of its own — and with no ETag
/// and no Last-Modified anywhere in this API, revalidating is a whole request.
/// So the project carried the risk of a shared cache on a cookie-authenticated
/// API and took none of the benefit, and nothing said so: a policy that never
/// applies looks exactly like a policy that works.
///
/// Textual because the header is a literal in a TypeScript object, and the
/// alternative — asserting on a live browser — would test the browser. What the
/// server's half of the same pair promises is checked from the wire, by
/// PublicCacheableResponsesShould.
/// </remarks>
public class ResponseCachePolicyShould
{
    /// <summary>A request header assignment, whatever quoting style is used.</summary>
    private static readonly Regex CacheControlHeader = new(
        @"[""']?Cache-Control[""']?\s*:", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    [Fact]
    public void NotBeCancelledByTheClientOnEveryRequest()
    {
        var client = Path.Combine(RepositoryRoot, "src", "DM.Web.Client", "src", "shared", "api", "client.ts");
        File.Exists(client).Should().BeTrue("the HTTP client is where the default headers live");

        var code = SourceText.ReadCode(client);

        CacheControlHeader.IsMatch(code).Should().BeFalse(
            "a Cache-Control request header set for every call decides caching for endpoints it " +
            "knows nothing about: `no-cache` on the request stops the server's store from being " +
            "read and makes the browser revalidate, which without a validator is a full request. " +
            "What may be cached is the origin's decision, and it states it per endpoint with " +
            "[ResponseCache]");
    }

    /// <summary>
    /// Every endpoint that may be stored by a cache the client does not own is
    /// covered by the client's freshness gate.
    /// </summary>
    /// <remarks>
    /// The four catalogues behind the profile were given a reload that asks the
    /// origin; the fifth, /v1/games/tags, was not, because the list of
    /// catalogues was written by hand from the four that had been looked at. It
    /// is written by a moderator through v1/moderation/tags — a different
    /// address, which invalidates nothing — and read by the tag picker, so a tag
    /// added on the moderation screen stayed invisible for five minutes and
    /// survived a reload doing it.
    ///
    /// So the list is derived here instead: the policy is read off the assembly,
    /// and a path that carries it and is missing from the client gate fails.
    /// A sixth catalogue cannot be forgotten the way the fifth was.
    /// </remarks>
    [Fact]
    public void CoverEveryPubliclyCacheableReadByTheClientsFreshnessGate()
    {
        var gate = Path.Combine(RepositoryRoot, "src", "DM.Web.Client", "src", "catalogFreshness.spec.ts");
        File.Exists(gate).Should().BeTrue("the client's freshness gate is what this check derives the list for");

        var declared = SharedCacheableReads();
        declared.Should().NotBeEmpty("the API declares a shared cache policy on its catalogues");

        var covered = File.ReadAllText(gate);
        var missing = declared
            .Where(read => !covered.Contains($"\"{read.Path}\"", StringComparison.Ordinal))
            .Select(read => $"{read.Action} answers {read.Path}")
            .ToArray();

        missing.Should().BeEmpty(
            "a response the browser and the shared cache may keep for {0} seconds is one nothing " +
            "invalidates: the write goes to v1/moderation/..., a different address. The screen that " +
            "edits it has to re-read it past both caches, and catalogFreshness.spec.ts is where that " +
            "is pinned — listing the path there is what says the reload exists", 300);
    }

    /// <summary>
    /// What a scanner rule is silenced with is what the code does.
    /// </summary>
    /// <remarks>
    /// .zap/rules.tsv switches off rule 10049, "Storable and Cacheable Content",
    /// with a sentence about a measure taken elsewhere: the reads that hand out
    /// the caller's own account state declare no-store. Nothing did - the only
    /// NoStore in the whole API sat on two catalogues and was put there for
    /// unread counts - so a scanner rule was switched off by pointing at
    /// something that did not exist, which is the way of triaging a finding that
    /// leaves no trace when it stops being true.
    ///
    /// Derived rather than listed: every authenticated read of the account area
    /// is asked, so the endpoint written next month is covered without anybody
    /// remembering this file. The area is the one the sentence is about; a rule
    /// over the whole API would be a decision about its cache policy, and that
    /// is not one a test may make on its own.
    /// </remarks>
    [Fact]
    public void KeepEveryAuthenticatedAccountReadOutOfEveryCache()
    {
        var reads = AuthenticatedAccountReads();

        reads.Should().NotBeEmpty("the account area answers reads that require a session");

        foreach (var (origin, policy) in reads)
        {
            policy.Should().NotBeNull(
                $"{origin} answers with the caller's own account state, and .zap/rules.tsv " +
                "silences rule 10049 by saying that those reads declare no-store");
            policy!.NoStore.Should().BeTrue($"{origin} must be stored by no cache");
            policy.Location.Should().Be(ResponseCacheLocation.None,
                $"{origin} must be stored by no cache, the browser's own included");
        }

        File.ReadAllText(Path.Combine(RepositoryRoot, ".zap", "rules.tsv")).Should().Contain("10049",
            "this check exists because that rule is silenced with the measure above; with the " +
            "rule back in force the scan reports the same thing on its own");
    }

    /// <summary>
    /// Every GET of the account area that requires a session, with whatever cache
    /// policy it declares.
    /// </summary>
    /// <remarks>
    /// The authentication attribute is internal to the host, so it is matched by
    /// name rather than by type: opening the host up so that a test project can
    /// see one attribute is the wrong shape of dependency for one line.
    /// </remarks>
    private static IReadOnlyCollection<(string Origin, ResponseCacheAttribute? Policy)> AuthenticatedAccountReads()
    {
        var found = new List<(string, ResponseCacheAttribute?)>();

        var controllers = typeof(DM.Web.API.Startup).Assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsPublic: true } &&
                        typeof(ControllerBase).IsAssignableFrom(t) &&
                        (t.Namespace ?? string.Empty).StartsWith(
                            "DM.Web.API.Features.Account", StringComparison.Ordinal));

        foreach (var controller in controllers)
        {
            var controllerRequiresSession = controller.GetCustomAttributes(inherit: true)
                .Any(a => a.GetType().Name == "AuthenticationRequiredAttribute");

            foreach (var action in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance |
                                                         BindingFlags.DeclaredOnly))
            {
                if (!action.GetCustomAttributes().OfType<HttpGetAttribute>().Any())
                {
                    continue;
                }

                var attributes = action.GetCustomAttributes(inherit: true);
                if (attributes.Any(a => a is IAllowAnonymous))
                {
                    continue;
                }

                var requiresSession = controllerRequiresSession ||
                                      attributes.Any(a => a.GetType().Name == "AuthenticationRequiredAttribute");
                if (!requiresSession)
                {
                    continue;
                }

                found.Add(($"{controller.Name}.{action.Name}",
                    action.GetCustomAttribute<ResponseCacheAttribute>(inherit: true)));
            }
        }

        return found;
    }

    /// <summary>An action the API lets any cache store, and the path it answers on.</summary>
    private sealed record CacheableRead(string Action, string Path);

    /// <summary>
    /// Reads the declared policy off the assembly rather than off the attribute
    /// text: the wire path is the route the framework composes, and composing it
    /// again from source text would be a second implementation of the same rule.
    /// </summary>
    private static CacheableRead[] SharedCacheableReads()
    {
        var found = new List<CacheableRead>();

        var controllers = typeof(DM.Web.API.Startup).Assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsPublic: true } &&
                        typeof(ControllerBase).IsAssignableFrom(t));

        foreach (var controller in controllers)
        {
            var prefix = controller.GetCustomAttribute<RouteAttribute>()?.Template ?? string.Empty;

            foreach (var action in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance |
                                                         BindingFlags.DeclaredOnly))
            {
                var policy = action.GetCustomAttribute<ResponseCacheAttribute>();
                if (policy is null || policy.Location != ResponseCacheLocation.Any || policy.Duration <= 0)
                {
                    continue;
                }

                var template = action.GetCustomAttributes()
                    .OfType<HttpGetAttribute>()
                    .FirstOrDefault()?.Template ?? string.Empty;

                var route = template.StartsWith('/')
                    ? template.TrimStart('/')
                    : string.Join('/', new[] { prefix, template }.Where(part => part.Length > 0));

                // The client's base URL already carries the version segment.
                var path = route.StartsWith("v1/", StringComparison.Ordinal) ? route[3..] : route;

                found.Add(new CacheableRead($"{controller.Name}.{action.Name}", path));
            }
        }

        return found.ToArray();
    }

    private static string RepositoryRoot => DM.Testing.RepositoryLayout.Root;
}
