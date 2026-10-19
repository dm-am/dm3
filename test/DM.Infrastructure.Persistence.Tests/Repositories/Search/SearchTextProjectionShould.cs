using System;
using System.Linq;
using DM.Infrastructure.Core.Parsing;
using DM.Infrastructure.Persistence.Repositories.Search;
using AwesomeAssertions;
using Xunit;

namespace DM.Infrastructure.Persistence.Tests.Repositories.Search;

/// <summary>
/// The visible text of a body, which is what gets indexed and what gets previewed.
/// </summary>
/// <remarks>The tag set differs between surfaces; [private] is a node on every one and dropped on every one.</remarks>
public class SearchTextProjectionShould
{
    [Fact]
    public void LeaveNoMarkupInTheTextItProjects()
    {
        var projected = SearchTextProjection.Of(
            "[b]Странники[/b] вышли к [i]реке[/i]", BbSurface.GamePost);

        projected.Should().Be("Странники вышли к реке");
    }

    /// <summary>
    /// The private block never becomes text, on the surface that has one.
    /// </summary>
    [Fact]
    public void DropWhatOnlyTheMasterWasMeantToRead()
    {
        var projected = SearchTextProjection.Of(
            "Отряд идет дальше [private]ловушка в третьей комнате[/private] к реке",
            BbSurface.GamePost);

        projected.Should().NotContain("ловушка");
        projected.Should().NotContain("private");
        projected.Should().Contain("Отряд идет дальше");
        projected.Should().Contain("к реке");
    }

    /// <summary>
    /// And it stays dropped when an unclosed tag above it reaches over the block.
    /// </summary>
    /// <remarks>Text swallowed into an image URL before the parse would never be a node the filter is asked about.</remarks>
    [Fact]
    public void DropItAlsoWhenAnUnclosedImageTagReachesOverTheBlock()
    {
        var projected = SearchTextProjection.Of(
            "Отряд идет дальше [img]https://example.com/a.png\n" +
            "[private=\"Гончая\"]ловушка в третьей комнате[/private]\n" +
            "[img]https://example.com/b.png[/img] к реке",
            BbSurface.GamePost);

        projected.Should().NotContain("ловушка");
        projected.Should().NotContain("private");
        // The public halves of the post are still indexed: the fix may not buy
        // the privacy back by dropping the author's text instead.
        projected.Should().Contain("Отряд идет дальше");
        projected.Should().Contain("к реке");
    }

    /// <summary>
    /// And it stays dropped under a block that shows nothing as an example: a
    /// [code] of any spelling, and a [noparse] written with a value or never
    /// closed.
    /// </summary>
    /// <remarks>
    /// A block with a value is a broken tag and an unclosed [noparse] no example, so what stands inside is read
    /// as text; [code] only changes the font. Every spelling of the hidden block counts.
    /// </remarks>
    [Theory]
    [InlineData("до [code=\"x\"][private=\"Гончая\"]ловушка[/private][/code] после")]
    [InlineData("до [noparse=\"x\"][private=\"Гончая\"]ловушка[/private][/noparse] после")]
    [InlineData("до [noparse=x][private]ловушка[/private][/noparse] после")]
    [InlineData("до [code=\"x\"][private=\"Гончая\"]ловушка")]
    [InlineData("до [code]пример [private=\"Гончая\"]ловушка[/private] после")]
    [InlineData("до [code][private=\"Гончая\"]ловушка[/private][/code] после")]
    [InlineData("до [noparse][private to=\"Гончая\"]ловушка")]
    [InlineData("до [private to=\"Гончая\"]ловушка[/private] после")]
    [InlineData("до [PRIVATE]ловушка[/private] после")]
    public void DropItUnderACodeBlockThatIsNotOneAndInEverySpelling(string body)
    {
        var projected = SearchTextProjection.Of(body, BbSurface.GamePost);

        projected.Should().NotContain("ловушка");
        projected.Should().Contain("до");
    }

    /// <summary>
    /// The characters a reader sees, not the entities of a page.
    /// </summary>
    /// <remarks>"a &lt; b" indexed as the page spells it could not be searched for; a typed entity stays.</remarks>
    [Fact]
    public void ReadBackTheEntitiesTheRendererWrote()
    {
        var projected = SearchTextProjection.Of("a < b & c &lt; d -- e", BbSurface.GlobalChatMessage);

        projected.Should().Be("a < b & c &lt; d \u2013 e");
    }

    /// <summary>
    /// A tag nobody closed is the text it is, and a hidden block under it is
    /// still dropped.
    /// </summary>
    /// <remarks>The page reads what follows an unclosed tag as text, so the block under it is hidden.</remarks>
    [Theory]
    [InlineData("до [b]пример [private=\"Гончая\"]ловушка[/private] после", "до [b]пример после")]
    [InlineData("до [code]пример [private=\"Гончая\"]ловушка[/private] после", "до [code]пример после")]
    [InlineData("до [noparse][private]ловушка[/private] после", "до [noparse] после")]
    [InlineData("до [quote]пример [PRIVATE]ловушка[/PRIVATE] после", "до [quote]пример после")]
    [InlineData("до [spoiler][private=\"Гончая\"]ловушка", "до [spoiler]")]
    public void KeepAnUnclosedTagAsTextAndTheHiddenBlockUnderItOut(string body, string expected)
    {
        SearchTextProjection.Of(body, BbSurface.GamePost).Should().Be(expected);
    }

    /// <summary>
    /// A hidden block off the game post is dropped from the text as well: the
    /// page erases it for every reader, so nobody can find it by its words.
    /// </summary>
    [Theory]
    [InlineData(BbSurface.Comment)]
    [InlineData(BbSurface.GlobalChatMessage)]
    public void DropAHiddenBlockOffTheGamePost(BbSurface surface)
    {
        SearchTextProjection.Of("до [private=\"Гончая\"]ловушка[/private] после", surface)
            .Should().Be("до после");
    }

    /// <summary>
    /// A body the parser refuses is indexed up to the first thing that could
    /// open a hidden block, read in the text the parser would have read.
    /// </summary>
    /// <remarks>
    /// The parser removes controls and reordering characters first, which joins what stood around them.
    /// Without the tree nothing says where a block ends (IndexNothingAfterABlockTheFallbackCannotEnd).
    /// </remarks>
    [Theory]
    [InlineData(0x0001)]
    [InlineData(0x202E)]
    [InlineData(0x2066)]
    public void CutTheHiddenBlockOutOfABodyTheParserRefused(int code)
    {
        var body = "до " + string.Concat(Enumerable.Repeat("[b]", 600)) +
                   "[pri" + (char)code + "vate]ловушка[/private] после";

        var projected = SearchTextProjection.Of(body, BbSurface.GamePost);

        projected.Should().NotContain("ловушка");
        projected.Should().StartWith("до ");
    }

    /// <summary>
    /// A body the parser refuses leaks no word of a block the pattern of the
    /// second lock would end too soon.
    /// </summary>
    /// <remarks>The second lock's pattern ends a block at the first [/private], nested or quoted as an example.</remarks>
    [Theory]
    [InlineData("[private]раз [private]два[/private] ловушка[/private]")]
    [InlineData("[private]раз [noparse][/private][/noparse] ловушка[/private]")]
    public void IndexNothingAfterABlockTheFallbackCannotEnd(string block)
    {
        var body = "до " + string.Concat(Enumerable.Repeat("[b]", 600)) + block + " после";

        SearchTextProjection.Of(body, BbSurface.GamePost).Should().NotContain("ловушка");
    }

    [Fact]
    public void TurnLineBreaksIntoOrdinarySpaces()
    {
        var projected = SearchTextProjection.Of(
            "первая строка\nвторая строка", BbSurface.GlobalChatMessage);

        projected.Should().Be("первая строка вторая строка");
    }

    /// <summary>
    /// Control characters out: the preview marks a match with two of them.
    /// </summary>
    /// <remarks>
    /// BbParser.Parse removes them for every consumer, so the words join as the page draws them; Collapse keeps
    /// the second lock for the fallback path.
    /// </remarks>
    [Fact]
    public void KeepControlCharactersOutOfTheProjection()
    {
        var projected = SearchTextProjection.Of("до\u0011после", BbSurface.GlobalChatMessage);

        projected.Should().Be("допосле");
    }

    [Fact]
    public void AnswerEmptyForABodyWithNothingInIt()
    {
        SearchTextProjection.Of(null, BbSurface.Comment).Should().BeEmpty();
        SearchTextProjection.Of("   ", BbSurface.Comment).Should().BeEmpty();
    }

    /// <summary>
    /// The text is one for every reader, and which files a reader may open is his question: no picture
    /// or link that names a file is indexed, in either spelling of its identifier, whoever it is for.
    /// A link elsewhere stays.
    /// </summary>
    [Theory]
    [InlineData("[img]https://dm.am/v1/uploads/{0:D}/content[/img]")]
    [InlineData("[IMG]/v1/uploads/{0:N}/content[/IMG]")]
    [InlineData("[link]https://dm.am/v1/uploads/{0:D}/content[/link]")]
    public void IndexNoPictureOrLinkOfAFile(string tag)
    {
        var id = Guid.NewGuid();
        var projected = SearchTextProjection.Of(
            $"Карта {string.Format(tag, id)} и [link]https://example.com/map[/link]", BbSurface.GamePost);

        projected.Should().Be("Карта и https://example.com/map");
    }

    /// <summary>And in a text the parser refuses, nothing from the first identifier on.</summary>
    [Fact]
    public void IndexNoFileOfATextTooDeepToParse()
    {
        var id = Guid.NewGuid();
        var body = $"до [img]/v1/uploads/{id:D}/content[/img] " + string.Concat(Enumerable.Repeat("[b]", 600)) + " после";

        var projected = SearchTextProjection.Of(body, BbSurface.GamePost);

        projected.Should().StartWith("до").And.NotContain(id.ToString("D")[..8]);
    }
}
