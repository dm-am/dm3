using System.Text.RegularExpressions;
using AwesomeAssertions;
using Xunit;
using static DM.Architecture.Tests.DeploymentFiles;

namespace DM.Architecture.Tests;

/// <remarks>
/// A worker drains its queue from the outbox table, so the table is what it waits for. A worker
/// started before the migration claims from a table that is not there and spends its loop errors
/// until the schema arrives.
/// </remarks>
public class WorkerStartShould
{
    /// <remarks>Mutation it must fail on: a worker that waits for anything but the finished migration.</remarks>
    [Theory]
    [InlineData("dm-mail-worker")]
    [InlineData("dm-notification-worker")]
    public void StartOnceTheMigrationHasFinished(string service)
    {
        var block = ServiceBlock(Read(BaseCompose), service);

        block.Should().MatchRegex(
            new Regex(@"depends_on:\s*\n(\s+#.*\n)*\s+migration:\s*\n\s+condition: service_completed_successfully"),
            $"{service} claims rows from the outbox table, which the migration creates");
    }
}
