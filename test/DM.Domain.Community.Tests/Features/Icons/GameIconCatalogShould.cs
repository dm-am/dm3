using DM.Domain.Community.Features.Icons;
using AwesomeAssertions;
using Xunit;

namespace DM.Domain.Community.Tests.Features.Icons;

public class GameIconCatalogShould
{
    [Theory]
    [InlineData("laurels", true)]
    [InlineData("unknown-icon", false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData(null, false)]
    public void KnowOnlyTheSpriteNames(string? iconName, bool expected) =>
        GameIconCatalog.IsValid(iconName!).Should().Be(expected);
}
