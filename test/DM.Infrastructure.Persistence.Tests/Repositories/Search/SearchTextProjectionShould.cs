using System.Linq;
using DM.Infrastructure.Core.Parsing;
using DM.Infrastructure.Persistence.Repositories.Search;
using AwesomeAssertions;
using Xunit;

namespace DM.Infrastructure.Persistence.Tests.Repositories.Search;

/// <summary>
/// The visible text of a body, which is what gets indexed and what gets previewed.
/// </summary>
/// <remarks>
/// Both used to read the raw body. The index tokenised markup, so a tag name was
/// a word one could search for; the preview cut the raw string at a fixed length,
/// so what the reader saw in the results was a fragment of BBCode, sometimes
/// ending mid-tag.
///
/// The surface is the part that has to be right rather than merely present: the
/// tag set differs between surfaces. [private] is a node on every one of them and
/// is dropped from the text on every one.
/// </remarks>
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
    /// <remarks>
    /// This is what the column held on live data. The [img] content pattern ran to
    /// the next [/img] anywhere in the body, so an image the author never closed
    /// joined up with the closing tag of the following one and everything between
    /// them — the whole private block — was filed as that image's URL before the
    /// parse. Filed there it is not a node, the visibility filter is never asked
    /// about it, and the text walk hands it back verbatim: the private line went
    /// into this column word for word, and from there into the window ts_headline
    /// opens around any other match in the same body.
    /// </remarks>
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
    /// [code="x"] used to open a block shown as written, value and all, so the
    /// private block inside it was an example, printed to the room and projected
    /// into this column word for word, while the save path saw an ordinary block
    /// and let the post through. A block with a value is a broken tag now, and
    /// what stands inside it is read like anything else; a [noparse] nobody
    /// closed is no example either, and what follows it is read the same way.
    /// [code] is no example at all, closed or not: it changes the font, as on
    /// DM2, so the hidden block inside it is a hidden block. Every spelling of
    /// the hidden block counts, since every one of them is a block on the page.
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
    public void DropItUnderACodeBlockThatIsNotOne_AndInEverySpelling(string body)
    {
        var projected = SearchTextProjection.Of(body, BbSurface.GamePost);

        projected.Should().NotContain("ловушка");
        projected.Should().Contain("до");
    }

    /// <summary>
    /// The characters a reader sees, not the entities of a page.
    /// </summary>
    /// <remarks>
    /// Indexed as the page spells it, a body reading "a &lt; b" is "a &amp;lt; b":
    /// nobody can search for it and nobody would recognise it. What the author
    /// typed as an entity stays the entity, since that is what the reader sees.
    /// </remarks>
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
    /// <remarks>
    /// The page prints an unclosed tag as it was typed and reads what follows it
    /// as any other text, [code] and [noparse] included, so the hidden block
    /// under one is a hidden block. The words after the block are public and
    /// searchable; the block is neither.
    /// </remarks>
    [Theory]
    [InlineData("до [b]пример [private=\"Гончая\"]ловушка[/private] после", "до [b]пример после")]
    [InlineData("до [code]пример [private=\"Гончая\"]ловушка[/private] после", "до [code]пример после")]
    [InlineData("до [noparse][private]ловушка[/private] после", "до [noparse] после")]
    [InlineData("до [quote]пример [PRIVATE]ловушка[/PRIVATE] после", "до [quote]пример после")]
    [InlineData("до [spoiler][private=\"Гончая\"]ловушка", "до [spoiler]")]
    public void KeepAnUnclosedTagAsText_AndTheHiddenBlockUnderItOut(string body, string expected)
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
    /// The parser removes control characters and the characters that reverse the
    /// text before it reads anything, and the removal joins what stood around
    /// them: "[pri" U+0001 "vate]" is a hidden block on the page. Read as it is
    /// stored it matched nothing, and the block went into the index word for
    /// word. Nested past the depth guard, the body takes the fallback path.
    ///
    /// Nothing after that point is indexed, the words after the block
    /// included: without the tree nothing says where a block ends
    /// (IndexNothingAfterABlockTheFallbackCannotEnd).
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
    /// <remarks>
    /// Found by the generated checks (SearchTextProjectionPropertiesShould). The
    /// fallback used to cut blocks out with that pattern, which ends a block at
    /// the first [/private] it meets: the end of a block inside the block, or a
    /// [/private] written as an example inside [noparse]. Everything the outer
    /// block held after that point went into the index.
    /// </remarks>
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
    /// They now come out one step earlier, in BbParser.Parse, which normalises
    /// its input for every consumer instead of leaving each to clean up after
    /// it. That is why the two words join rather than being separated by the
    /// space this used to expect: the parser removes the character, and removing
    /// it is what keeps the projected text equal to the rendered page - a C0
    /// control between two words is drawn as nothing, so a reader saw the words
    /// joined all along while the index held them apart. The local pass in
    /// Collapse stays as the second lock: it is also what runs on the fallback
    /// path, where the parser refused the body and the raw source is projected.
    /// </remarks>
    [Fact]
    public void KeepControlCharactersOutOfTheProjection()
    {
        var projected = SearchTextProjection.Of("допосле", BbSurface.GlobalChatMessage);

        projected.Should().Be("допосле");
    }

    [Fact]
    public void AnswerEmptyForABodyWithNothingInIt()
    {
        SearchTextProjection.Of(null, BbSurface.Comment).Should().BeEmpty();
        SearchTextProjection.Of("   ", BbSurface.Comment).Should().BeEmpty();
    }
}
