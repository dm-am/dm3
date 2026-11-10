using System.Linq;
using System.Security.Cryptography;
using System.Text;
using DM.Domain.Account.Features.TwoFactor;
using AwesomeAssertions;
using Xunit;

namespace DM.Domain.Account.Tests.Features.TwoFactor;

// The entropy is asserted as well, because it is the whole argument for a fast hash: the secret of
// the factor is shut behind the application key, so a recovery code is what a reader of a database
// dump attacks without one, and eighty bits is what makes that not worth doing.
public class RecoveryCodeFactoryShould
{
    [Fact]
    public void IssueAsManyCodesAsAsked() =>
        RecoveryCodeFactory.Create(10).Should().HaveCount(10);

    [Fact]
    public void IssueCodesOfEightyBits()
    {
        var codes = RecoveryCodeFactory.Create(10);

        codes.Should().OnlyContain(code => code.Length == RecoveryCodeFactory.CodeLength,
            "sixteen symbols out of an alphabet of thirty-two is eighty bits, and the number " +
            "is calculated rather than picked: forty bits over ten codes is hours of one " +
            "graphics card against a dump");
    }

    [Fact]
    public void IssueCodesFromTheAlphabetWithoutTheLookalikes()
    {
        var codes = RecoveryCodeFactory.Create(50);

        string.Concat(codes).Should().NotContainAny("I", "L", "O", "U");
    }

    [Fact]
    public void IssueADifferentSetEveryTime()
    {
        var first = RecoveryCodeFactory.Create(10);
        var second = RecoveryCodeFactory.Create(10);

        first.Intersect(second).Should().BeEmpty();
        first.Distinct().Should().HaveCount(10);
    }

    [Theory]
    [InlineData("abcd-efgh-jkmn-pqrs", "ABCDEFGHJKMNPQRS")]
    [InlineData("ABCD EFGH JKMN PQRS", "ABCDEFGHJKMNPQRS")]
    [InlineData("aBcD-eFgH JkMn.PqRs", "ABCDEFGHJKMNPQRS")]
    public void ForgiveCaseAndSeparators(string typed, string expected) =>
        RecoveryCodeFactory.Normalize(typed).Should().Be(expected,
            "a code copied onto paper and typed back with dashes must be the same code");

    [Theory]
    [InlineData("1", "I")]
    [InlineData("1", "l")]
    [InlineData("0", "O")]
    public void FoldTheLookalikesOntoTheDigits(string expected, string typed) =>
        RecoveryCodeFactory.Normalize(typed).Should().Be(expected,
            "the alphabet leaves these letters out precisely because a handwritten code " +
            "loses them to the digits beside them");

    [Fact]
    public void StoreOnlyTheHashOfTheValue()
    {
        var code = RecoveryCodeFactory.Create(1)[0];

        RecoveryCodeFactory.Hash(code).Should().Equal(SHA256.HashData(Encoding.UTF8.GetBytes(code)));
    }

    [Fact]
    public void HashTheSameCodeTheSameWayHoweverItWasTyped()
    {
        var code = RecoveryCodeFactory.Create(1)[0];
        var grouped = string.Join('-', Enumerable.Range(0, 4)
            .Select(i => code.Substring(i * RecoveryCodeFactory.GroupSize, RecoveryCodeFactory.GroupSize)));

        RecoveryCodeFactory.Hash(grouped.ToLowerInvariant()).Should().Equal(RecoveryCodeFactory.Hash(code));
    }

    [Fact]
    public void HashDifferentCodesDifferently()
    {
        var codes = RecoveryCodeFactory.Create(2);

        RecoveryCodeFactory.Hash(codes[0]).Should().NotEqual(RecoveryCodeFactory.Hash(codes[1]));
    }

    [Fact]
    public void NormalizeNothingToNothing() =>
        RecoveryCodeFactory.Normalize(string.Empty).Should().BeEmpty();
}
