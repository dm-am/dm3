using DM.Domain.Account.Features.Security;
using FluentAssertions;
using Xunit;

namespace DM.Domain.Account.Tests.Features.Security;

public class SaltFactoryShould
{
    private readonly SaltFactory _factory;

    public SaltFactoryShould()
    {
        _factory = new SaltFactory();
    }

    [Fact]
    public void CreateNonEmptySalt()
    {
        var result = _factory.Create(32);

        result.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void CreateSaltWithMinimumLength()
    {
        var saltLength = 32;

        var result = _factory.Create(saltLength);

        result.Length.Should().BeGreaterOrEqualTo(saltLength);
    }

    [Fact]
    public void CreateDifferentSalts()
    {
        var salt1 = _factory.Create(32);
        var salt2 = _factory.Create(32);

        salt1.Should().NotBe(salt2);
    }

    [Fact]
    public void CreateValidBase64String()
    {
        var result = _factory.Create(32);

        var isValidBase64 = System.Convert.TryFromBase64String(result, new byte[result.Length], out _);
        isValidBase64.Should().BeTrue();
    }
}
