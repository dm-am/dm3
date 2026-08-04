using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using FluentAssertions;
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
    /// <summary>Line and block comments, in the syntax of the client's sources.</summary>
    private static readonly Regex Comments = new(
        @"/\*.*?\*/|//[^\n]*", RegexOptions.Compiled | RegexOptions.Singleline);

    /// <summary>A request header assignment, whatever quoting style is used.</summary>
    private static readonly Regex CacheControlHeader = new(
        @"[""']?Cache-Control[""']?\s*:", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    [Fact]
    public void NotBeCancelledByTheClientOnEveryRequest()
    {
        var client = Path.Combine(RepositoryRoot, "src", "DM.Web.Client", "src", "shared", "api", "client.ts");
        File.Exists(client).Should().BeTrue("the HTTP client is where the default headers live");

        var code = Comments.Replace(File.ReadAllText(client), string.Empty);

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

    /// <summary>
    /// Walks up from the test binary to the repository root: the sources are not
    /// copied to the output directory, and copying them would assert against a
    /// stale snapshot.
    /// </summary>
    private static string RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "docs")))
            {
                directory = directory.Parent;
            }

            directory.Should().NotBeNull("the repository root must be above the test binary");
            return directory!.FullName;
        }
    }
}
