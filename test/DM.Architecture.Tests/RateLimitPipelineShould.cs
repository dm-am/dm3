using System;
using System.IO;
using AwesomeAssertions;
using DM.Testing;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// The limiter counts per account only because the pipeline puts the account
/// there first.
/// </summary>
/// <remarks>
/// A partitioner is called synchronously, so the account must already be on the context; the limiter
/// stays above authentication so a flood costs no session lookup. Source text: a wrong order still
/// starts and still limits, only not whom it says.
/// </remarks>
public class RateLimitPipelineShould
{

    [Fact]
    public void NameTheAccountBeforeTheLimiterCountsAgainstIt()
    {
        // Read as code: a commented-out Startup must not satisfy the comparisons.
        var startup = SourceText.ReadCode(
            Path.Combine(RepositoryLayout.RootDirectory.FullName, "src", "DM.Web.API", "Startup.cs"));

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

    /// <summary>
    /// Every request leaves one line, and only one.
    /// </summary>
    /// <remarks>
    /// Microsoft logs are cut to Warning, so this is the only request line. Below the error handler it
    /// would log each failure twice, above the correlation middleware without a token.
    /// </remarks>
    [Fact]
    public void WriteOneLinePerRequestBelowTheTokenAndAboveTheHandler()
    {
        var startup = SourceText.ReadCode(
            Path.Combine(RepositoryLayout.RootDirectory.FullName, "src", "DM.Web.API", "Startup.cs"));

        var correlation = startup.IndexOf(
            "UseMiddleware<CorrelationMiddleware>()", StringComparison.Ordinal);
        var logging = startup.IndexOf("UseSerilogRequestLogging(", StringComparison.Ordinal);
        var errors = startup.IndexOf(
            "UseMiddleware<ErrorHandlingMiddleware>()", StringComparison.Ordinal);

        correlation.Should().BeGreaterThan(-1, "the token is part of the pipeline");
        errors.Should().BeGreaterThan(-1, "the error handler is part of the pipeline");
        logging.Should().BeGreaterThan(-1,
            "without it nothing records that a request happened, and the framework's own " +
            "record is cut by the source override that keeps its noise out of the store");

        correlation.Should().BeLessThan(logging,
            "a line without the correlation token cannot be tied to the refusal it explains");
        logging.Should().BeLessThan(errors,
            "below the handler every failure is written twice, in two different wordings");
    }

    [Fact]
    public void SayInTheContractWhatABudgetIsCountedPer()
    {
        var document = File.ReadAllText(Path.Combine(
            RepositoryLayout.RootDirectory.FullName, "docs", "conventions", "API_DESIGN.md"));

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
