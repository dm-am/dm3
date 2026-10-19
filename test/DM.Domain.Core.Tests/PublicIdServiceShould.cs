using System;
using System.Collections.Generic;
using DM.Domain.Core.Identity;
using AwesomeAssertions;
using Xunit;

namespace DM.Domain.Core.Tests;

/// <summary>
/// The encoding every readable page address on the site is built out of.
/// </summary>
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
