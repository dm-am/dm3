using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using DM.Testing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// The cache policy the server declares is a policy the only consumer can obey.
/// </summary>
/// <remarks>
/// A request-side `no-cache` forbids the middleware its store and makes the browser revalidate, and this
/// API has no ETag. Textual: the header is a literal in a TypeScript object. The wire half is
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
        var client = Path.Combine(RepositoryLayout.Root, "src", "DM.Web.Client", "src", "shared", "api", "client.ts");
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
    /// <remarks>Derived from the assembly: a catalogue missing from the client gate fails.</remarks>
    [Fact]
    public void CoverEveryPubliclyCacheableReadByTheClientsFreshnessGate()
    {
        var gate = Path.Combine(RepositoryLayout.Root, "src", "DM.Web.Client", "src", "gates", "catalogFreshness.spec.ts");
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
    /// .zap/rules.tsv silences rule 10049 by pointing at no-store on the account reads. Derived: every
    /// authenticated read of the account area is asked.
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
    }

    /// <summary>
    /// Every GET of the account area that requires a session, with whatever cache
    /// policy it declares.
    /// </summary>
    /// <remarks>The authentication attribute is internal to the host, so it is matched by name.</remarks>
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
    /// Reads the declared policy off the assembly: the wire path is the route the framework composes.
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
}
