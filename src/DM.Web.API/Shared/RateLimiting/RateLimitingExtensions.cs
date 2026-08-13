using System;
using System.Threading.RateLimiting;
using DM.Web.API.Middleware;
using DM.Web.API.Shared.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DM.Web.API.Shared.RateLimiting;

/// <summary>
/// Registration of every rate-limit policy of the API.
/// </summary>
/// <remarks>
/// The policies are declared as data and registered in a loop. Written out one
/// by one they were seven near-identical lambdas repeating the same partition
/// key expression, plus a second list of seven no-op registrations for the
/// disabled case — so adding a policy meant two edits, and forgetting the
/// second one made every test hitting that endpoint answer 429.
/// </remarks>
internal static class RateLimitingExtensions
{
    /// <summary>Every limiter here counts within the same window.</summary>
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    /// <summary>Requests one address may make across the whole API per window.</summary>
    private const int GlobalPermitLimit = 100;

    /// <summary>What a limiter counts separately.</summary>
    internal enum Partition
    {
        /// <summary>By peer address. For endpoints reachable without a session.</summary>
        Address,

        /// <summary>
        /// By account, falling back to the address for guests. Keeps one noisy
        /// account from consuming the budget of everyone behind a shared address,
        /// and keeps one account from multiplying its budget by moving between
        /// addresses. The account is the one the session cookie names, put on the
        /// request by <see cref="RateLimitAccountMiddleware" /> — see it for why
        /// it cannot be read here.
        /// </summary>
        AccountThenAddress,
    }

    /// <param name="Name">Policy name controllers ask for.</param>
    /// <param name="Partition">What the permits are counted per.</param>
    /// <param name="PermitLimit">Requests allowed per <see cref="Window"/>.</param>
    /// <param name="SlidingSegments">
    /// Segments per window; zero selects a fixed window. A sliding window costs
    /// more to track and only pays off where a burst at a window boundary is the
    /// thing being prevented.
    /// </param>
    private sealed record Policy(string Name, Partition Partition, int PermitLimit, int SlidingSegments = 0);

    private static readonly Policy[] Policies =
    {
        // Credential endpoints. Deliberately the strictest number here: this is
        // the budget an online password-guessing attempt gets.
        new(RateLimitPolicies.Auth, Partition.Address, 5),

        // Registration-form availability checks. Reachable without a session and
        // they answer a question about other people's data, so they are counted
        // per address and kept well under the global budget.
        new(RateLimitPolicies.UsernameCheck, Partition.Address, 20),
        new(RateLimitPolicies.EmailCheck, Partition.Address, 10),

        // Anti-flood on top of the per-file size limit: valid small files still
        // cost storage and image processing.
        new(RateLimitPolicies.Uploads, Partition.AccountThenAddress, 10),

        // Expensive reads. Search runs tsvector scans against the primary OLTP
        // database, which is a more realistic cost vector than the other read
        // paths, and a sliding window denies it the boundary burst.
        new(RateLimitPolicies.Sliding, Partition.AccountThenAddress, 30, SlidingSegments: 6),

        // Ordinary authenticated writes. Tighter than the global budget because
        // these are per-account paths.
        new(RateLimitPolicies.Default, Partition.AccountThenAddress, 60),
    };

    /// <summary>
    /// Whether this deployment counts requests at all.
    /// </summary>
    /// <remarks>
    /// One reading of the setting, and one default. The registration below and
    /// whatever else asks - the startup warning, today - have to agree on what an
    /// absent key means, and a second literal true is how the two come to disagree
    /// silently: a deployment would be limited and reported as unlimited, or the
    /// other way round, with nothing anywhere saying which.
    /// </remarks>
    /// <param name="configuration">Configuration to read the setting from.</param>
    internal static bool IsEnabled(IConfiguration configuration) =>
        configuration.GetValue("RateLimiting:Enabled", true);

    /// <summary>
    /// Adds the rate limiter with every policy of the API.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configuration">
    /// Read for <c>RateLimiting:Enabled</c>. When it is false every policy is
    /// still registered, as a no-op limiter: the policies have to resolve by
    /// name whether or not they count anything.
    /// </param>
    public static IServiceCollection AddDmRateLimiting(
        this IServiceCollection services, IConfiguration configuration)
    {
        var enabled = IsEnabled(configuration);

        services.AddRateLimiter(options =>
        {
            if (enabled)
            {
                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        PartitionKey(context, Partition.Address),
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = GlobalPermitLimit,
                            Window = Window,
                            QueueLimit = 0,
                        }));

                foreach (var policy in Policies)
                {
                    options.AddPolicy(policy.Name, context => Limiter(policy, context));
                }
            }
            else
            {
                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(_ =>
                    RateLimitPartition.GetNoLimiter<string>("unlimited"));

                foreach (var policy in Policies)
                {
                    options.AddPolicy(policy.Name, _ =>
                        RateLimitPartition.GetNoLimiter<string>("unlimited"));
                }
            }

            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.OnRejected = async (context, ct) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

                context.HttpContext.Response.Headers.RetryAfter =
                    context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
                        ? ((int)retryAfter.TotalSeconds).ToString()
                        : ((int)Window.TotalSeconds).ToString();

                // Built by the same factory as every other error response. Hand
                // assembled here, this was the one body on the wire that did not
                // match what the rest of the API answers with — no traceId, and a
                // shape nothing else produced.
                var factory = context.HttpContext.RequestServices
                    .GetRequiredService<ProblemDetailsFactory>();
                var problem = factory.CreateProblemDetails(
                    context.HttpContext,
                    StatusCodes.Status429TooManyRequests,
                    "Слишком много запросов",
                    detail: "Лимит запросов превышен. Повторите позже.");

                // The content type goes through WriteAsJsonAsync: assigning
                // Response.ContentType before it is overwritten, which is why
                // every 429 went out as application/json while the rest of the
                // API answered application/problem+json.
                await context.HttpContext.Response.WriteAsJsonAsync(
                    problem, problem.GetType(), options: null, contentType: "application/problem+json", ct);
            };
        });

        return services;
    }

    private static RateLimitPartition<string> Limiter(Policy policy, HttpContext context)
    {
        var key = PartitionKey(context, policy.Partition);

        return policy.SlidingSegments > 0
            ? RateLimitPartition.GetSlidingWindowLimiter(key, _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = policy.PermitLimit,
                Window = Window,
                SegmentsPerWindow = policy.SlidingSegments,
                QueueLimit = 0,
            })
            : RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = policy.PermitLimit,
                Window = Window,
                QueueLimit = 0,
            });
    }

    /// <summary>Whose budget one request spends.</summary>
    /// <remarks>
    /// HttpContext.User is empty in this project — no authentication scheme is
    /// registered and the identity lives in IIdentityProvider — so the account
    /// asked of it was always null and every "per account" policy counted per
    /// address instead. The address comes from the shared helper rather than
    /// straight off the connection, so that a dual-stack peer is one caller here
    /// in one spelling, the same as it is in the login journal. The two answers
    /// share a namespace and are therefore spelled apart: nothing about an
    /// address may name an account by coincidence.
    /// </remarks>
    internal static string PartitionKey(HttpContext context, Partition partition)
    {
        var account = partition == Partition.AccountThenAddress
            ? RateLimitAccountMiddleware.Account(context)
            : null;

        return account.HasValue
            ? $"account:{account.Value}"
            : $"address:{context.GetClientAddress()}";
    }
}
