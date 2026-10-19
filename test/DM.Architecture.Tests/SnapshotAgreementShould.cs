using System.Linq;
using AwesomeAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// The two generated descriptions of the model say the same thing.
/// </summary>
/// <remarks>
/// The snapshot is what the next migration is diffed against, the designer is what `migrations remove`
/// restores. A hand edit to InitialCreate can reach one and miss the other, and nothing else reads the
/// designer. Compared from the generated body down: the headers differ by design.
/// </remarks>
public class SnapshotAgreementShould
{
    [Fact]
    public void DescribeOneModelInBothGeneratedFiles()
    {
        var snapshots = SchemaSources.Snapshots.ToList();

        snapshots.Should().HaveCount(2,
            "a migration ships with a designer file and the context snapshot, and finding " +
            "fewer means this rule is comparing something with itself");

        foreach (var (name, text) in snapshots)
        {
            text.Should().Contain(SchemaSources.ModelStart,
                $"{name} is generated, and a body this rule cannot find is one it cannot compare");
        }

        var (firstName, first) = snapshots[0];
        var (secondName, second) = snapshots[1];

        SchemaSources.Model(first).Should().Be(SchemaSources.Model(second),
            $"{firstName} and {secondName} are generated from one model in one pass, so a " +
            "difference between them is a hand edit that reached one and not the other - and " +
            "the half nobody reads is the half the next `migrations remove` would restore");
    }
}
