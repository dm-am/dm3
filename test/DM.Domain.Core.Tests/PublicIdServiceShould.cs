using System;
using System.Collections.Generic;
using DM.Domain.Core.Identity;
using AwesomeAssertions;
using Xunit;

namespace DM.Domain.Core.Tests;

/// <summary>
/// The encoding every readable page address on the site is built out of.
/// </summary>
/// <remarks>
/// It used to live in the mirror of DM.Infrastructure.Core, which is not the
/// project that declares it: the kernel had no test project at all, so its one
/// covered type was tested from somewhere else.
/// </remarks>
public class PublicIdServiceShould
{
    private readonly PublicIdService service = new();

    [Theory]
    [InlineData(1, "aaaaa")]  // All 'a' = first serial
    [InlineData(2, "aaaab")]  // Second letter
    [InlineData(23, "aaaaz")] // Last single-digit (z = position 22, 0-indexed)
    [InlineData(24, "aaaba")] // First two-digit (overflow to second position)
    public void EncodeCorrectly(int serialNumber, string expected)
    {
        var result = service.Encode(serialNumber);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("aaaaa", 1)]
    [InlineData("aaaab", 2)]
    [InlineData("aaaaz", 23)]
    [InlineData("aaaba", 24)]
    public void DecodeCorrectly(string publicId, int expected)
    {
        var result = service.Decode(publicId);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(42)]
    [InlineData(1000)]
    [InlineData(100000)]
    [InlineData(1000000)]
    public void RoundtripCorrectly(int serialNumber)
    {
        var encoded = service.Encode(serialNumber);
        var decoded = service.Decode(encoded);
        decoded.Should().Be(serialNumber);
    }

    [Theory]
    [InlineData("aaaaa")]
    [InlineData("abcde")]
    [InlineData("zzzzz")]
    [InlineData("kxmnt")]
    public void ValidateCorrectFormats(string publicId)
    {
        service.IsValid(publicId).Should().BeTrue();
    }

    [Theory]
    [InlineData("")] // Empty
    [InlineData("aaaa")] // Too short (4 chars)
    [InlineData("aaa")] // Too short (3 chars)
    [InlineData("AAAAA")] // Uppercase
    [InlineData("aaaia")] // Contains 'i'
    [InlineData("aaala")] // Contains 'l'
    [InlineData("aaaoa")] // Contains 'o'
    [InlineData("aaa1a")] // Contains digit
    [InlineData("aaa-a")] // Contains hyphen
    public void RejectInvalidFormats(string publicId)
    {
        service.IsValid(publicId).Should().BeFalse();
    }

    [Fact]
    public void ThrowOnZeroSerialNumber()
    {
        var act = () => service.Encode(0);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ThrowOnNegativeSerialNumber()
    {
        var act = () => service.Encode(-1);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ThrowOnInvalidPublicIdDecode()
    {
        var act = () => service.Decode("invalid");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ProduceFiveLetterCodes()
    {
        for (var i = 1; i <= 100; i++)
        {
            var encoded = service.Encode(i);
            encoded.Should().HaveLength(5);
            encoded.Should().MatchRegex("^[a-z]{5}$");
        }
    }

    [Fact]
    public void ProduceUniqueCodesForDifferentSerials()
    {
        var codes = new HashSet<string>();
        for (var i = 1; i <= 1000; i++)
        {
            var code = service.Encode(i);
            codes.Add(code).Should().BeTrue($"Serial {i} produced duplicate code {code}");
        }
    }
}
