using System;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <remarks>
/// Both cap the wait so that an outage is not followed by minutes of silence; the client's
/// ladder is read out of its source, the host's is called.
/// </remarks>
public class ReconnectLadderShould
{
    [Fact]
    public void WaitAsLongAsTheHostBeforeEachAttempt()
    {
        var ladder = Regex.Match(ClientCopies.Source("shared/api/client.ts"),
            @"Math\.min\((?<cap>\d+), (?<first>\d+) \* 2 \*\* context\.previousRetryCount\)");
        ladder.Success.Should().BeTrue("hubRetryPolicy doubles a first wait up to a cap");

        var cap = double.Parse(ladder.Groups["cap"].Value, CultureInfo.InvariantCulture);
        var first = double.Parse(ladder.Groups["first"].Value, CultureInfo.InvariantCulture);

        var host = typeof(DM.Web.API.Startup).Assembly
            .GetType("DM.Web.API.Realtime.PushListener", throwOnError: true)!
            .GetMethod("DefaultRetryDelay", BindingFlags.NonPublic | BindingFlags.Static)!;

        for (var attempt = 1; attempt <= 8; attempt++)
        {
            var client = Math.Min(cap, first * Math.Pow(2, attempt - 1));
            ((TimeSpan)host.Invoke(null, [attempt])!).TotalMilliseconds.Should().Be(client,
                $"attempt {attempt} waits as long on both sides of the socket");
        }
    }
}
