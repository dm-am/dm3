using AwesomeAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// The parts of the DM2 description are left out of the rules for DM3's own text, and nothing else is: the folder's
/// README, a note the folder gains later and a file in a folder below it are our own text.
/// </summary>
public class HandwrittenDocumentsShould
{
    [Theory]
    [InlineData("docs/references/dm2/1-site-layout.md", true)]
    [InlineData("docs/references/dm2/4-games-discussion-closing.md", true)]
    [InlineData("docs/references/dm2/README.md", false)]
    [InlineData("docs/references/dm2/decisions.md", false)]
    [InlineData("docs/references/dm2/notes/1-site-layout.md", false)]
    [InlineData("docs/references/1-site-layout.md", false)]
    public void LeaveOutOfTheTextRulesOnlyThePartsOfTheDm2Description(string path, bool part) =>
        HandwrittenDocuments.DescribesForeignSite(path).Should().Be(part, $"{path} is {(part ? "" : "not ")}a part");
}
