using System.Linq;
using DM.Infrastructure.Persistence.Repositories.Search;
using FluentAssertions;
using Xunit;

namespace DM.Infrastructure.Persistence.Tests.Repositories.Search;

/// <summary>
/// The window the database marked, split into the runs a reader sees.
/// </summary>
/// <remarks>
/// The preview used to be the first two hundred characters of the body, so the
/// words that were searched for were usually not in it — and could not have been
/// found by looking either, because the search matches by lexeme: a query for
/// "странник" matches "странников", and no substring of the query occurs in it.
/// So the window and the mark both come from the database, and this is where the
/// marks stop being characters and become a shape the contract can carry.
///
/// The sentinels are control characters, and the projection strips control
/// characters out of every body: a visible pair would be one an author could type,
/// and the preview would then split where nobody searched.
/// </remarks>
public class SearchSnippetHighlightShould
{
    private const char Start = '';
    private const char End = '';

    [Fact]
    public void SplitTheWindowAroundWhatWasMatched()
    {
        var segments = SearchSnippet.Highlight($"мимо шли {Start}странников{End} к реке");

        segments.Select(s => (s.Text, s.IsMatch)).Should().Equal(
            ("мимо шли ", false),
            ("странников", true),
            (" к реке", false));
    }

    [Fact]
    public void CarryEveryMatchOfTheWindow()
    {
        var segments = SearchSnippet.Highlight(
            $"{Start}странник{End} и еще один {Start}странник{End}");

        segments.Count(s => s.IsMatch).Should().Be(2);
        segments.Should().OnlyContain(s => s.Text.Length > 0,
            "an empty run is a segment the reader cannot see and the client still renders");
    }

    /// <summary>
    /// The marks themselves never reach anybody.
    /// </summary>
    [Fact]
    public void LeaveNoMarkInTheTextItHandsOver()
    {
        var segments = SearchSnippet.Highlight($"до {Start}тут{End} после");

        segments.Should().OnlyContain(s => !s.Text.Contains(Start) && !s.Text.Contains(End));
    }

    [Fact]
    public void AnswerNothingForAWindowThatIsNotThere()
    {
        SearchSnippet.Highlight(null).Should().BeEmpty();
        SearchSnippet.Highlight("").Should().BeEmpty();
    }

    /// <summary>
    /// A search with no words in it has no match to build a window around.
    /// </summary>
    /// <remarks>
    /// Filtering by author or by date matches every row equally, so the beginning
    /// of the text is the honest preview - and it is one unmarked run, not a
    /// pretence that something was found.
    /// </remarks>
    [Fact]
    public void FallBackToThePlainBeginningWithNothingMarked()
    {
        var segments = SearchSnippet.Plain("Отряд идет дальше к реке");

        segments.Should().ContainSingle();
        segments[0].IsMatch.Should().BeFalse();
        segments[0].Text.Should().Be("Отряд идет дальше к реке");
    }

    [Fact]
    public void KeepThePrivateBlockOutOfTheFallbackAsWell()
    {
        var segments = SearchSnippet.Plain(
            "Отряд идет [private]ловушка в третьей комнате[/private] дальше");

        segments.Should().ContainSingle();
        segments[0].Text.Should().NotContain("ловушка");
    }
}
