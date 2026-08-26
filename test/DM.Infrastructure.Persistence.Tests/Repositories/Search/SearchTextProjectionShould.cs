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
/// The surface is the part that has to be right rather than merely present.
/// [private] is a node of the tree only where the surface declares it — rendered
/// on a surface that does not, the block survives as a literal, and its contents
/// go into the index and into every preview of it.
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
    /// The renderer answers with entities and with tags for its line breaks.
    /// </summary>
    /// <remarks>
    /// Left as they come, a body reading "a &lt; b" is indexed and previewed as
    /// "a &amp;lt; b": nobody can search for it and nobody would recognise it.
    /// </remarks>
    [Fact]
    public void ReadBackTheEntitiesTheRendererWrote()
    {
        var projected = SearchTextProjection.Of("a < b", BbSurface.GlobalChatMessage);

        projected.Should().Be("a < b");
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
