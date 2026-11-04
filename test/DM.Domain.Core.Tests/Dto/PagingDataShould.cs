using DM.Domain.Core.Dto;
using AwesomeAssertions;
using Xunit;

namespace DM.Domain.Core.Tests.Dto;

/// <summary>
/// The page a query is on is the page it is on, all the way to the top of the
/// range the query may ask for.
/// </summary>
/// <remarks>
/// Skip goes up to int.MaxValue and becomes an entity number by adding one, which must not wrap.
/// </remarks>
public class PagingDataShould
{
    [Fact]
    public void StayPastTheFirstPageAtTheTopOfTheRangeSkipAllows()
    {
        var paging = new PagingData(new PagingQuery { Skip = int.MaxValue, Take = 20 }, 20, 1000);

        paging.Result.CurrentPage.Should().BeGreaterThan(1,
            "an offset of two billion is past the first page by any reading, and the answer " +
            "that it is page one is the arithmetic having wrapped rather than a page");
        paging.Skip.Should().Be(int.MaxValue,
            "what the query asked to skip is what the query asked to skip");
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(19, 1)]
    [InlineData(20, 2)]
    [InlineData(40, 3)]
    public void CountThePageFromTheEntityTheOffsetLandsOn(int skip, int expected) =>
        new PagingData(new PagingQuery { Skip = skip, Take = 20 }, 20, 1000)
            .Result.CurrentPage.Should().Be(expected,
                "the ordinary arithmetic has to survive the guard against the extraordinary");
}
