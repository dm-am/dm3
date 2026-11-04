using System;
using System.Threading.RateLimiting;
using DM.Domain.Core.Exceptions;
using DM.Web.API.Middleware;
using DM.Web.API.Shared.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DM.Web.API.Shared.RateLimiting;

/// <summary>
/// Registration of every rate-limit policy of the API.
/// </summary>
/// <remarks>
/// The policies are data registered in one loop, so a new one is one line.
/// </remarks>
internal static class RateLimitingExtensions
{
    /// <summary>Every limiter here counts within the same window.</summary>
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Requests one address may make across the whole API per window.
    /// </summary>
    /// <remarks>
    /// A backstop against scraping and floods, sized for an address that is a
    /// neighbourhood behind carrier-grade NAT rather than one person.
    /// </remarks>
    private const int GlobalPermitLimit = 300;

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
    internal sealed record Policy(string Name, Partition Partition, int PermitLimit, int SlidingSegments = 0);

    /// <summary>
    /// The whole table, readable by the tests that gate which endpoint spends
    /// which of these.
    /// </summary>
    internal static readonly Policy[] Policies =
    {
        // The strictest: the budget of an online password-guessing attempt.
        new(RateLimitPolicies.Auth, Partition.Address, 5),

        // Per account, since these carry a session; ten because one sitting is four
        // requests and a mistyped code must cost a refusal that says so.
        new(RateLimitPolicies.TwoFactor, Partition.AccountThenAddress, 10),

        // Reachable without a session and answering about other people's data.
        new(RateLimitPolicies.UsernameCheck, Partition.Address, 20),
        new(RateLimitPolicies.EmailCheck, Partition.Address, 10),

        // Valid small files still cost storage and image processing.
        new(RateLimitPolicies.Uploads, Partition.AccountThenAddress, 10),

        // Search scans the primary database; a sliding window denies the boundary burst.
        new(RateLimitPolicies.Sliding, Partition.AccountThenAddress, 30, SlidingSegments: 6),

        new(RateLimitPolicies.Default, Partition.AccountThenAddress, 60),
    };

    /// <summary>
    /// Whether this deployment counts requests at all: the one reading of the
    /// setting and of its default.
    /// </summary>
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
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context => enabled
                ? Fixed(PartitionKey(context, Partition.Address), GlobalPermitLimit)
                : Unlimited());

            foreach (var policy in Policies)
            {
                options.AddPolicy(policy.Name, context => enabled ? Limiter(policy, context) : Unlimited());
            }

            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.OnRejected = async (context, ct) =>
            {
                var retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var leaseRetryAfter)
                    ? leaseRetryAfter
                    : Window;
                context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();

                // The factory of every other error response, so the body has a traceId.
                var factory = context.HttpContext.RequestServices
                    .GetRequiredService<ProblemDetailsFactory>();
                var problem = factory.CreateProblemDetails(
                    context.HttpContext,
                    StatusCodes.Status429TooManyRequests,
                    RefusalMessage.TooManyRequests,
                    detail: RefusalMessage.RateLimitExceeded);

                // The contentType overload: WriteAsJsonAsync overwrites a ContentType set before it.
                await context.HttpContext.Response.WriteAsJsonAsync(
                    problem, problem.GetType(), options: null, contentType: ApiContentTypes.ProblemJson, ct);
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
            : Fixed(key, policy.PermitLimit);
    }

    private static RateLimitPartition<string> Fixed(string key, int permitLimit) =>
        RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = Window,
            QueueLimit = 0,
        });

    private static RateLimitPartition<string> Unlimited() => RateLimitPartition.GetNoLimiter("unlimited");

    /// <summary>Whose budget one request spends.</summary>
    /// <remarks>
    /// The account comes from <see cref="RateLimitAccountMiddleware" />, since
    /// HttpContext.User is empty here, and the address from the shared helper, so
    /// a dual-stack peer is one caller. The two are spelled apart in one namespace,
    /// so an address never names an account by coincidence.
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
