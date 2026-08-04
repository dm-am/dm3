using DM.Infrastructure.Core.Randomness;
using FluentAssertions;
using Xunit;

namespace DM.Infrastructure.Core.Tests.Randomness;

public class RandomNumberGeneratorShould
{
    private readonly RandomNumberGenerator generator = new();

    [Fact]
    public void GenerateNumberInRange()
    {
        var actual = generator.Generate(5, 10);
        actual.Should().BeInRange(5, 10);
    }

    [Fact]
    public void GenerateDieRoll()
    {
        var actual = generator.Generate(10);
        actual.Should().BeInRange(1, 10);
    }
}
