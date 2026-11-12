using System.Linq;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Xunit;
using static DM.Architecture.Tests.DeploymentFiles;

namespace DM.Architecture.Tests;

/// <remarks>
/// The rules read a family only the release script writes, and promtool tests each rule on
/// series it is handed: a field the script stopped writing, or never wrote, passes those tests
/// and never fires on a server.
/// </remarks>
public class ReleaseAlertsShould
{
    private static readonly Regex Field = new(@"dm_release\{field=""([a-z_]+)""", RegexOptions.Compiled);

    [Fact]
    public void ReadOnlyFieldsTheReleaseScriptWrites()
    {
        var rules = Read("prometheus/alerts.yml");
        var script = Read("scripts/release-entry.sh");

        var fields = Field.Matches(rules).Select(match => match.Groups[1].Value).Distinct().ToList();

        fields.Should().BeEquivalentTo(new[] { "changed", "failed", "waiting_since", "fetched", "rejected", "built" },
            "the six facts of the release design 3.15 are what the rules read");
        foreach (var field in fields)
        {
            (script.Contains($"field=\"{field}\"", System.StringComparison.Ordinal)
             || script.Contains($"field=\\\"{field}\\\"", System.StringComparison.Ordinal))
                .Should().BeTrue($"a rule reads dm_release{{field=\"{field}\"}}, which release-entry.sh must write");
        }
    }
}
