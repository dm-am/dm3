using System;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// The two generated descriptions of the model say the same thing.
/// </summary>
/// <remarks>
/// A migration ships with two of them: the designer file beside it, which is what
/// the tooling reads as "the model this migration produced", and the context
/// snapshot, which is what the next migration is diffed against. Both are
/// generated from the same model in the same pass, so agreement is not a
/// property to be maintained — it is what regeneration produces.
///
/// Hand-editing is what pulls them apart, and hand-editing is how this schema is
/// changed while there is nothing to preserve. Three passes over the schema
/// updated the snapshot and left the designer behind: it still described a token
/// column that had been replaced by its hash, two indexes that had been dropped,
/// and an older spelling of a generated column. Nothing failed — the drift-check
/// reads the snapshot, the database is built from the migration, and the designer
/// is read by exactly one command, `migrations remove`, which would have restored
/// the stale model as the new truth.
///
/// Compared from the generated body down, because the headers differ by design:
/// one is a partial of the migration and carries its attribute, the other is the
/// snapshot class.
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
