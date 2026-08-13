using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DM.Domain.Account.Configuration;
using FluentAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// The delay a wrong password costs stays under the timeout of the client asking.
/// </summary>
/// <remarks>
/// It did not. The top step was thirty seconds and the client gives up at thirty,
/// so from the tenth attempt on the answer never arrived: what the reader got was
/// a toast about the connection dropping instead of "wrong password" — the site
/// saying it is broken to somebody who is simply mistyping, and saying it for
/// half a minute of spinner first.
///
/// Neither number fails anything on its own, and they live in different languages
/// in different projects, so nothing but a rule can hold them together. The rule
/// is a ratio rather than a pair of literals: the point is the gap, and copying
/// either number here would make this file the third place to change.
/// </remarks>
public class LoginDelayCeilingShould
{
    /// <summary>The client gives up on a request after this many seconds.</summary>
    private static double ClientTimeoutSeconds()
    {
        var client = File.ReadAllText(Path.Combine(DM.Testing.RepositoryLayout.Root,
            "src", "DM.Web.Client", "src", "shared", "api", "client.ts"));

        var timeout = Regex.Match(client, @"timeout:\s*(\d+)");
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

    /// <summary>
    /// A schedule that only ever goes up, and a first step that costs nothing.
    /// </summary>
    /// <remarks>
    /// Thresholds out of order silently shadow each other — the lookup takes the
    /// first match — and a delay on the first attempt is a delay every reader pays
    /// on the day they mistype once.
    /// </remarks>
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
