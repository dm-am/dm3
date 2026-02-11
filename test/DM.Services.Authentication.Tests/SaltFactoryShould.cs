using System;
using DM.Services.Authentication.Implementation.Security;
using FluentAssertions;
using Xunit;

namespace DM.Services.Authentication.Tests;

public class SaltFactoryShould
{
    private readonly SaltFactory saltFactory = new();

    [Fact]
    public void ProduceBase64StringOfAtLeastRequestedLength()
    {
        var result = saltFactory.Create(32);

        result.Length.Should().BeGreaterOrEqualTo(32);
    }

    [Fact]
    public void ProduceValidBase64String()
    {
        var result = saltFactory.Create(32);

        // This will throw if the string is not valid Base64
        var act = () => Convert.FromBase64String(result);

        act.Should().NotThrow();
    }

    [Fact]
    public void ProduceDifferentSaltsOnEachCall()
    {
        var salt1 = saltFactory.Create(32);
        var salt2 = saltFactory.Create(32);

        salt1.Should().NotBe(salt2);
    }

    [Fact]
    public void HandleSmallSaltLength()
    {
        var result = saltFactory.Create(1);

        result.Length.Should().BeGreaterOrEqualTo(1);

        // Verify it's valid Base64
        var act = () => Convert.FromBase64String(result);
        act.Should().NotThrow();
    }

    [Fact]
    public void HandleLargeSaltLength()
    {
        var result = saltFactory.Create(128);

        result.Length.Should().BeGreaterOrEqualTo(128);

        // Verify it's valid Base64
        var act = () => Convert.FromBase64String(result);
        act.Should().NotThrow();
    }
}