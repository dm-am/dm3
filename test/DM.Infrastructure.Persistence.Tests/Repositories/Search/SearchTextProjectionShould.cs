using DM.Infrastructure.Core.Parsing;
using DM.Infrastructure.Persistence.Repositories.Search;
using FluentAssertions;
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
    [Fact]
    public void KeepControlCharactersOutOfTheProjection()
    {
        var projected = SearchTextProjection.Of("допосле", BbSurface.GlobalChatMessage);

        projected.Should().Be("до после");
    }

    [Fact]
    public void AnswerEmptyForABodyWithNothingInIt()
    {
        SearchTextProjection.Of(null, BbSurface.Comment).Should().BeEmpty();
        SearchTextProjection.Of("   ", BbSurface.Comment).Should().BeEmpty();
    }
}
