using DM.Domain.Core.Dto;
using FluentAssertions;
using Xunit;

namespace DM.Domain.Core.Tests.Dto;

/// <summary>
/// Paging arithmetic: how many pages a total splits into, which page an entity
/// number falls on, and the floor under that page.
/// </summary>
/// <remarks>
/// No mock factory base class, and these tests need none: the type under test is
/// a calculation with no collaborators. The kernel's test project references the
/// kernel and nothing else, and the base class lives in DM.Testing, which pulls
/// DM.Infrastructure.Core and DM.Infrastructure.Persistence in behind it — the
/// one thing this project exists to keep out.
/// </remarks>
public class PagingResultShould
{
    [Theory]
    [InlineData(1000, 10, 100)]
    [InlineData(1001, 10, 101)]
    [InlineData(999, 10, 100)]
    [InlineData(0, 10, 0)]
    public void CalculateTotalPagesCountOfGivenSize(int totalEntitiesCount, int pageSize, int expectedPagesCount)
    {
        var actual = PagingResult.Create(totalEntitiesCount, 1, pageSize);
        actual.TotalPagesCount.Should().Be(expectedPagesCount);
    }

    [Theory]
    [InlineData(1, 10, 1)]
    [InlineData(10, 10, 1)]
    [InlineData(11, 10, 2)]
    [InlineData(25, 10, 3)]
    public void CalculateCurrentPageBasedOnGivenEntityNumberAndPageSize(int entityNumber,
        int pageSize, int expectedPageNumber)
    {
        var actual = PagingResult.Create(entityNumber + 1, entityNumber, pageSize);
        actual.CurrentPage.Should().Be(expectedPageNumber);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    [InlineData(1000)]
    [InlineData(-4)]
    [InlineData(5)]
    public void GuaranteeCurrentPageIsAtLeastFirst(int entityNumber)
    {
        var actual = PagingResult.Create(100, entityNumber, 10);
        actual.CurrentPage.Should().BeGreaterOrEqualTo(1);
    }
}
