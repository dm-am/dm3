using AwesomeAssertions;
using DM.Infrastructure.Core.Parsing;
using Xunit;
using static DM.Infrastructure.Core.Tests.BbAudienceRender;

namespace DM.Infrastructure.Core.Tests;

public class BbPrivateTagShould
{
    // A tag attribute is author text, so it is encoded where the tag writes it.
    [Fact]
    public void EncodePrivateAddresseeInTheRecipients()
    {
        var html = Html(BbSurface.GamePost, "[private=\"<img src=x onerror=alert(1)>\"]t[/private]");

        html.Should().NotContain("<img src=x onerror=alert(1)>");
        html.Should().Contain(
            "data-recipients=\"DungeonMaster, Assistant, &lt;img src=x onerror=alert(1)&gt;\">t</span>");
    }

    // The save path refuses such text, so what reaches the page was stored before it or carried
    // from DM2.
    [Theory]
    [InlineData(BbSurface.Comment)]
    [InlineData(BbSurface.GlobalChatMessage)]
    [InlineData(BbSurface.Profile)]
    [InlineData(BbSurface.DirectMessage)]
    public void EraseHidingMarkupWhereItDoesNotWork(BbSurface surface)
    {
        const string bbCode = "до [private=\"Вася\"]тайна[/private] после";

        Html(surface, bbCode).Should().Be("до  после");
        EmbedSafe(surface, bbCode).Should().Be("до  после");
    }

    /// <summary>
    /// The reader still gets the recipients for the tooltip; they are no text of the line, so the block is
    /// the green words and nothing after them.
    /// </summary>
    [Fact]
    public void GiveAReaderTheRecipientsOnTheBlockAndNoLineAfterIt()
    {
        Html(BbSurface.GamePost, "до [private=\"Вася\"]тайна[/private] после")
            .Should().Be(
                "до <span class=\"private-message\" data-recipients=\"DungeonMaster, Assistant, Вася\">тайна</span> после");
    }

    // The recipients name the master and the assistants first, then the block's names, a lead not
    // twice; the names are the list the save path reads.
    [Theory]
    [InlineData("[private=\"Лиара Тенебраум\"]тайна[/private]", "DungeonMaster, Assistant, Лиара Тенебраум")]
    [InlineData("[private=\"Assistant, Вася, dungeonmaster\"]тайна[/private]", "DungeonMaster, Assistant, Вася")]
    [InlineData("[private=\"DungeonMaster\"]тайна[/private]", "DungeonMaster, Assistant")]
    [InlineData("[private=\"Вася ,  Петя,\"]тайна[/private]", "DungeonMaster, Assistant, Вася, Петя")]
    public void NameTheLeadsFirstThenTheNamesOfTheBlock(string bbCode, string recipients)
    {
        Html(BbSurface.GamePost, bbCode)
            .Should().Contain($"<span class=\"private-message\" data-recipients=\"{recipients}\">");
    }

    // A block naming nobody is read by the master and the assistants, named in Latin letters as DM2
    // does, as plain text.
    [Theory]
    [InlineData("[private]тайна[/private]")]
    [InlineData("[private=\"\"]тайна[/private]")]
    [InlineData("[private=\" , \"]тайна[/private]")]
    [InlineData("[private=Вася]тайна[/private]")]
    public void NameTheLeadsInTheRecipientsOfABlockThatNamesNobody(string bbCode)
    {
        Html(BbSurface.GamePost, bbCode)
            .Should().Contain("<span class=\"private-message\" data-recipients=\"DungeonMaster, Assistant\">");
    }

    // Encoding does not reach what the reader sees: an ampersand arrives as one character.
    [Fact]
    public void KeepPrivateAddresseeWhenItIsAnOrdinaryName()
    {
        Html(BbSurface.GamePost, "[private=\"Вася & Петя\"]t[/private]")
            .Should().Contain("data-recipients=\"DungeonMaster, Assistant, Вася &amp; Петя\"");
    }
}
