using DM.Domain.Core.Content;
using DM.Infrastructure.Persistence.Repositories.Search;
using FluentAssertions;
using Xunit;

namespace DM.Infrastructure.Persistence.Tests.Repositories.Search;

/// <summary>
/// What a search preview may show of a post that has private blocks in it.
/// </summary>
/// <remarks>
/// Snippets are built from the raw stored BBCode, past the viewer-scoped render
/// that hides private text, so the cut here is the only thing between a private
/// line and every reader of the results page. The pattern it cuts by is shared
/// with the stored search vector — PrivateBlockPatternShould asserts that
/// PostgreSQL reads the same string the same way.
/// </remarks>
public class PrivateBlockSnippetShould
{
    [Theory]
    [InlineData("до [private]тайна[/private] после", "до после")]
    [InlineData("[private=Вася]тайна[/private] хвост", " хвост")]
    [InlineData("[private=\"Вася, Петя\"]тайна[/private] хвост", " хвост")]
    [InlineData("до [PRIVATE]тайна[/PRIVATE] после", "до после")]
    public void RemoveThePrivateBlockAndCollapseWhatIsLeft(string input, string expected)
    {
        SearchSnippet.StripPrivateBlocks(input).Should().Be(expected);
    }

    /// <summary>
    /// Text standing between two private blocks is public and stays. It used to
    /// disappear in PostgreSQL and survive here, which is the drift this pair of
    /// tests exists to prevent.
    /// </summary>
    [Fact]
    public void KeepPublicTextBetweenTwoBlocks()
    {
        SearchSnippet.StripPrivateBlocks("A [private]x[/private] СЕРЕДИНА [private]y[/private] B")
            .Should().Be("A СЕРЕДИНА B");
    }

    /// <summary>
    /// One string, not two copies that happen to agree today.
    /// </summary>
    [Fact]
    public void ShareThePatternWithTheStoredIndex()
    {
        SearchSnippet.PrivateBlockPattern.Should().BeSameAs(PrivateBlockMarkup.BlockPattern);
    }
}
