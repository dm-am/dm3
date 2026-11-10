using System.Linq;
using DM.Infrastructure.Persistence.Repositories.Search;
using AwesomeAssertions;
using Xunit;

namespace DM.Infrastructure.Persistence.Tests.Repositories.Search;

// The search matches by lexeme, so the window and the mark come from the database.
public class SearchSnippetHighlightShould
{
    private const char Start = '\u0011';
    private const char End = '\u0012';

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

    // Filtering by author or date matches every row equally, so the start of the text is one
    // unmarked run.
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
