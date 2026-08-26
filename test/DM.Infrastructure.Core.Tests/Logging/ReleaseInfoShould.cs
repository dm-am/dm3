using System.Reflection;
using DM.Infrastructure.Core.Logging;
using AwesomeAssertions;
using Xunit;

namespace DM.Infrastructure.Core.Tests.Logging;

/// <summary>
/// The commit a process was built from, or an honest admission that nobody stamped one.
/// </summary>
/// <remarks>
/// The value is read once from the entry assembly, so the reader itself cannot be
/// driven from a test — what can, and what carries the whole risk, is the parsing:
/// the stamp arrives as build metadata on the informational version, and every way
/// of getting it wrong produces a string that looks like an answer. An assembly
/// version with no stamp reads "1.0.0", which is the same on every build ever made
/// and would send whoever is reading the log to the wrong deployment.
/// </remarks>
public class ReleaseInfoShould
{
    [Theory]
    [InlineData("1.0.0+9f2c1ab", "9f2c1ab")]
    [InlineData("1.0.0", "unknown")]
    [InlineData("1.0.0+", "unknown")]
    [InlineData("", "unknown")]
    [InlineData(null, "unknown")]
    public void ReadTheCommitOffTheVersionOrSaySoPlainly(string? informational, string expected)
    {
        Parse(informational).Should().Be(expected);
    }

    [Fact]
    public void AnswerSomethingEvenWhereNothingWasStamped()
    {
        // Tests run against an assembly nobody stamps, so this is the "unknown"
        // branch end to end - and the property being non-empty is what the log
        // enricher depends on.
        ReleaseInfo.Value.Should().NotBeNullOrWhiteSpace();
    }

    /// <summary>
    /// The same parsing the property runs, reached through the private method so the
    /// cases above do not need an assembly each.
    /// </summary>
    private static string Parse(string? informational) =>
        (string)typeof(ReleaseInfo)
            .GetMethod("Parse", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [informational])!;
}
