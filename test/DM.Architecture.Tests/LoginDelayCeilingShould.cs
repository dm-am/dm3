using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DM.Domain.Account.Configuration;
using AwesomeAssertions;
using Xunit;

namespace DM.Architecture.Tests;

// The numbers live in different languages and projects, so the rule is a ratio, not a pair of
// literals.
public class LoginDelayCeilingShould
{
    private static double ClientTimeoutSeconds()
    {
        var client = File.ReadAllText(Path.Combine(DM.Testing.RepositoryLayout.Root,
            "src", "DM.Web.Client", "src", "shared", "api", "interceptor.ts"));

        var timeout = Regex.Match(client, @"(?:timeout:|REQUEST_TIMEOUT_MS\s*=)\s*(\d+)");
        timeout.Success.Should().BeTrue("the client declares how long it waits");

        return int.Parse(timeout.Groups[1].Value) / 1000d;
    }

    [Fact]
    public void StayWellUnderTheTimeOfTheCallerWaitingForIt()
    {
        var ceiling = (double)new AuthenticationConfiguration().LoginDelaySchedule
            .Select(step => step[1])
            .Max();

        var timeout = ClientTimeoutSeconds();

        ceiling.Should().BeLessThan(timeout / 2,
            "at the timeout the answer never arrives at all, and near it the reader is " +
            "told the site is broken rather than that the password is wrong; the lockout " +
            "is what stops guessing, and these seconds are paid by whoever mistyped");
    }

    // The lookup takes the first match, so thresholds out of order shadow each other.
    [Fact]
    public void GrowWithTheAttemptsAndStartFree()
    {
        var schedule = new AuthenticationConfiguration().LoginDelaySchedule;

        schedule.Should().NotBeEmpty("guessing has to get more expensive than not guessing");
        schedule.Should().OnlyContain(step => step.Length == 2,
            "each entry is a threshold and the seconds it costs");
        schedule[0][0].Should().BeGreaterThan(1,
            "the first mistyped password is the common case, not the attack");

        schedule.Select(step => step[0]).Should().BeInAscendingOrder(
            "the lookup takes the first threshold that matches, so an unordered schedule " +
            "shadows its own steps");
        schedule.Select(step => step[1]).Should().BeInAscendingOrder(
            "a step that costs less than the one before it is a step nobody meant");
    }
}
