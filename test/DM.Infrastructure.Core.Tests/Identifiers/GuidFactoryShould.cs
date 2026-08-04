using DM.Infrastructure.Core.Identifiers;
using FluentAssertions;
using Xunit;

namespace DM.Infrastructure.Core.Tests.Identifiers;

public class GuidFactoryShould
{
    private readonly GuidFactory guidFactory = new();

    [Fact]
    public void GenerateDifferentIds()
    {
        guidFactory.Create().Should().NotBe(guidFactory.Create());
    }

    [Fact]
    public void NotGenerateEmptyGuid()
    {
        guidFactory.Create().Should().NotBeEmpty();
    }
}