using System;
using DM.Domain.Account.Features.Security;
using AwesomeAssertions;
using Xunit;

namespace DM.Domain.Account.Tests.Features.Security;

public class SaltFactoryShould
{
    [Fact]
    public void CreateSaltWithMinimumLength()
    {
        var saltLength = 32;

        var result = SaltFactory.Create(saltLength);

        result.Length.Should().BeGreaterThanOrEqualTo(saltLength);
    }

    [Fact]
    public void CreateDifferentSalts()
    {
        var salt1 = SaltFactory.Create(32);
        var salt2 = SaltFactory.Create(32);

        salt1.Should().NotBe(salt2);
    }

    [Fact]
    public void CreateValidBase64String()
    {
        var result = SaltFactory.Create(32);

        var isValidBase64 = Convert.TryFromBase64String(result, new byte[result.Length], out _);
        isValidBase64.Should().BeTrue();
    }
}
