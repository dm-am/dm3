using DM.Infrastructure.Core.Logging;
using AwesomeAssertions;
using Xunit;

namespace DM.Infrastructure.Core.Tests.Logging;

// The value is read once from the entry assembly, so the parsing is tested; a version with no stamp
// reads "1.0.0" on every build.
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
        ReleaseInfo.Parse(informational).Should().Be(expected);
    }

    [Fact]
    public void AnswerSomethingEvenWhereNothingWasStamped()
    {
        // Tests run against an assembly nobody stamps, so this is the "unknown"
        // branch end to end - and the property being non-empty is what the log
        // enricher depends on.
        ReleaseInfo.Value.Should().NotBeNullOrWhiteSpace();
    }
}
