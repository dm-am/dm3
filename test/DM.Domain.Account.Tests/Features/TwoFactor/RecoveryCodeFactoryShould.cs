using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using DM.Domain.Account.Features.TwoFactor;
using AwesomeAssertions;
using Xunit;

namespace DM.Domain.Account.Tests.Features.TwoFactor;

/// <summary>
/// The values that let a person in when the device is gone.
/// </summary>
/// <remarks>
/// INV-4: a code is stored only as the SHA-256 of its value. The entropy is
/// asserted as well, because it is the whole argument for a fast hash: the secret
/// of the factor is shut behind the application key, so a recovery code is what a
/// reader of a database dump attacks without one, and eighty bits is what makes
/// that not worth doing.
/// </remarks>
public class RecoveryCodeFactoryShould
{
    private readonly RecoveryCodeFactory _factory = new();

    [Fact]
    public void IssueAsManyCodesAsAsked() =>
        _factory.Create(10).Should().HaveCount(10);

    [Fact]
    public void IssueCodesOfEightyBits()
    {
        var codes = _factory.Create(10);

        codes.Should().OnlyContain(code => code.Length == RecoveryCodeFactory.CodeLength,
            "sixteen symbols out of an alphabet of thirty-two is eighty bits, and the number " +
            "is calculated rather than picked: forty bits over ten codes is hours of one " +
            "graphics card against a dump");
    }

    [Fact]
    public void IssueCodesFromTheAlphabetWithoutTheLookalikes()
    {
        var codes = _factory.Create(50);

        string.Concat(codes).Should().NotContainAny("I", "L", "O", "U");
    }

    [Fact]
    public void IssueADifferentSetEveryTime()
    {
        var first = _factory.Create(10);
        var second = _factory.Create(10);

        first.Intersect(second).Should().BeEmpty();
        first.Distinct().Should().HaveCount(10);
    }

    [Theory]
    [InlineData("abcd-efgh-jkmn-pqrs", "ABCDEFGHJKMNPQRS")]
    [InlineData("ABCD EFGH JKMN PQRS", "ABCDEFGHJKMNPQRS")]
    [InlineData("aBcD-eFgH JkMn.PqRs", "ABCDEFGHJKMNPQRS")]
    public void ForgiveCaseAndSeparators(string typed, string expected) =>
        _factory.Normalize(typed).Should().Be(expected,
            "a code copied onto paper and typed back with dashes must be the same code");

    [Theory]
    [InlineData("1", "I")]
    [InlineData("1", "l")]
    [InlineData("0", "O")]
    public void FoldTheLookalikesOntoTheDigits(string expected, string typed) =>
        _factory.Normalize(typed).Should().Be(expected,
            "the alphabet leaves these letters out precisely because a handwritten code " +
            "loses them to the digits beside them");

    /// <summary>
    /// INV-4: what the database keeps is the SHA-256 of the normalized value.
    /// </summary>
    [Fact]
    public void StoreOnlyTheHashOfTheValue()
    {
        var code = _factory.Create(1)[0];

        _factory.Hash(code).Should().Equal(SHA256.HashData(Encoding.UTF8.GetBytes(code)));
    }

    [Fact]
    public void HashTheSameCodeTheSameWayHoweverItWasTyped()
    {
        var code = _factory.Create(1)[0];
        var grouped = string.Join('-', Enumerable.Range(0, 4)
            .Select(i => code.Substring(i * RecoveryCodeFactory.GroupSize, RecoveryCodeFactory.GroupSize)));

        _factory.Hash(grouped.ToLowerInvariant()).Should().Equal(_factory.Hash(code));
    }

    [Fact]
    public void HashDifferentCodesDifferently()
    {
        var codes = _factory.Create(2);

        _factory.Hash(codes[0]).Should().NotEqual(_factory.Hash(codes[1]));
    }

    [Fact]
    public void NormalizeNothingToNothing() =>
        _factory.Normalize(string.Empty).Should().BeEmpty();
}
