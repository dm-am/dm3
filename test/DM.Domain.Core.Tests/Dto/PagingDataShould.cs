using DM.Domain.Core.Dto;
using FluentAssertions;
using Xunit;

namespace DM.Domain.Core.Tests.Dto;

/// <summary>
/// The page a query is on is the page it is on, all the way to the top of the
/// range the query may ask for.
/// </summary>
/// <remarks>
/// Skip is bound as [0, int.MaxValue] and turned into a 1-based entity number by
/// adding one. At the top of that range the addition wrapped: the number came out
/// negative, the page was clamped back to one, and the envelope answered "page 1,
/// skip 0" to a request that had just run an offset of two billion and found
/// nothing. Nothing anywhere disagreed — the request succeeded, the list was
/// empty, and the answer described the beginning of the list.
///
/// Rare, and cheap to hold: the arithmetic is one expression shared by every
/// paged read in the site.
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
