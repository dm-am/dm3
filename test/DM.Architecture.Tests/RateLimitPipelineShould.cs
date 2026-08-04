using System;
using System.IO;
using FluentAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// The limiter counts per account only because the pipeline puts the account
/// there first.
/// </summary>
/// <remarks>
/// A rate-limit partitioner is handed an HttpContext and called synchronously,
/// so whatever it counts by has to be on that context already. One middleware
/// puts the account there, reading the session cookie and nothing else. Drop it,
/// or let it drift below the limiter, and every account-partitioned policy falls
/// back to counting per address without a word — no error, no log line, no
/// metric, just an office of a hundred people sharing ten uploads a minute
/// again. That is the failure this replaced, and it is invisible from outside.
///
/// The other half of the order pulls the opposite way and matters as much: the
/// limiter stays above authentication, so a flood is refused before it costs a
/// session lookup. That constraint is the entire reason the account is read out
/// of the cookie instead of taken from the identity, and a later reader who does
/// not know it will "simplify" the pipeline straight back into the problem.
///
/// Source text is the only surface: an order of middleware is not expressible in
/// types, and a pipeline assembled wrongly still starts, still answers, and
/// still limits — just not whom it says it does.
/// </remarks>
public class RateLimitPipelineShould
{
    /// <summary>
    /// Walks up from the test binary to the repository root. Neither the sources
    /// nor the documents are copied to the output directory, and copying them
    /// would let this assert against a stale snapshot.
    /// </summary>
    private static DirectoryInfo RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "docs")))
            {
                directory = directory.Parent;
            }

            directory.Should().NotBeNull("the repository root must be above the test binary");
            return directory!;
        }
    }

    [Fact]
    public void NameTheAccountBeforeTheLimiterCountsAgainstIt()
    {
        var startup = File.ReadAllText(
            Path.Combine(RepositoryRoot.FullName, "src", "DM.Web.API", "Startup.cs"));

        var account = startup.IndexOf(
            "UseMiddleware<RateLimitAccountMiddleware>()", StringComparison.Ordinal);
        var limiter = startup.IndexOf("UseRateLimiter()", StringComparison.Ordinal);
        var authentication = startup.IndexOf("UseAuthentication()", StringComparison.Ordinal);

        limiter.Should().BeGreaterThan(-1, "the limiter is part of the pipeline");
        authentication.Should().BeGreaterThan(-1, "authentication is part of the pipeline");
        account.Should().BeGreaterThan(-1,
            "without it the partitioner has nothing but the address to count by, and " +
            "every per-account policy becomes a per-address one in silence");

        account.Should().BeLessThan(limiter,
            "a partition key can only be built out of what is on the context by then");
        limiter.Should().BeLessThan(authentication,
            "a flood has to be refused before it costs a session lookup");
    }

    [Fact]
    public void SayInTheContractWhatABudgetIsCountedPer()
    {
        var document = File.ReadAllText(Path.Combine(
            RepositoryRoot.FullName, "docs", "conventions", "API_DESIGN.md"));

        var start = document.IndexOf("## Rate Limiting", StringComparison.Ordinal);
        start.Should().BeGreaterThan(-1, "the published contract states the budgets");

        var end = document.IndexOf("\n## ", start + 1, StringComparison.Ordinal);
        var section = end < 0 ? document[start..] : document[start..end];

        section.Should().Contain("по аккаунту",
            "a number per minute says nothing until the document says whose minute it is");
        section.Should().NotContain("а не по аккаунту",
            "the document described the defect as the rule, and the next endpoint would " +
            "be written to match it");
    }
}
