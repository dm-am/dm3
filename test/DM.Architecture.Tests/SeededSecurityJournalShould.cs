using System.Linq;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using DM.Tools.Seeder.Seeding;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// The journal lines the seed writes about an administrator's removal of a second factor are the
/// lines the removal service writes, word for word.
/// </summary>
/// <remarks>
/// The seed copies them, since writing them through the service would date them now. Read out of the
/// source: when the service words them otherwise, this fails until the copy in DataSeeder.TwoFactor.cs
/// follows, and a stand looked at before and after the change shows the change.
/// </remarks>
public class SeededSecurityJournalShould
{
    private static readonly Regex RemovalLine = new(
        @"SecurityEventType\.TwoFactorRemovedByAdmin,\s*details:\s*\$""(?<line>[^""]*)""", RegexOptions.Compiled);

    [Fact]
    public void WriteTheLinesTheRemovalServiceWrites()
    {
        var service = RepositoryFiles.Read("src/DM.Domain.Account/Features/TwoFactor", "TwoFactorRemovalService.cs");
        var lines = RemovalLine.Matches(service).Select(m => m.Groups["line"].Value).ToList();

        lines.Should().HaveCount(2, "the service writes the removal into two journals, the owner's and the administrator's");
        lines[0].Replace("{caller.Username}", "Admin1")
            .Should().Be(DataSeeder.RemovedByAdminOwnerDetails("Admin1"), "the owner's line");
        lines[1].Replace("{account.Username}", "Member1")
            .Should().Be(DataSeeder.RemovedByAdminDetails("Member1"), "the administrator's line");
    }
}
