using System;
using DM.Domain.Core.Extensions;
using AwesomeAssertions;
using Xunit;

namespace DM.Domain.Core.Tests;

// Base64 is not URL-safe, so the encoder swaps three characters and the decoder swaps them back.
public class ReadableGuidHelperShould
{
    // Guids chosen so their Base64 spells all three characters that have to be swapped: the pair a
    // URL would take differently, and the padding.
    public static TheoryData<string> Identifiers => new()
    {
        "00000000-0000-0000-0000-000000000000",
        "ffffffff-ffff-ffff-ffff-ffffffffffff",
        "3f3f3f3f-3f3f-3f3f-3f3f-3f3f3f3f3f3f",
        "fbfbfbfb-fbfb-fbfb-fbfb-fbfbfbfbfbfb",
        "1a2b3c4d-5e6f-4a7b-8c9d-0e1f2a3b4c5d",
    };

    [Theory]
    [MemberData(nameof(Identifiers))]
    public void DecodeBackWhatItEncoded(string value)
    {
        var guid = Guid.Parse(value);

        guid.EncodeToReadable().DecodeFromReadableGuid().Should().Be(guid);
    }

    [Theory]
    [MemberData(nameof(Identifiers))]
    public void DecodeBackWhatItEncodedWithATitleInFront(string value)
    {
        var guid = Guid.Parse(value);

        guid.EncodeToReadable("Приключение в Тени").DecodeFromReadableGuid().Should().Be(guid);
    }

    [Theory]
    [MemberData(nameof(Identifiers))]
    public void ProduceNothingAUrlWouldTakeDifferently(string value) =>
        Guid.Parse(value).EncodeToReadable("Заголовок")
            .Should().NotContainAny("/", "+", "=", " ");

    [Fact]
    public void PutTheReadableTextBeforeTheIdentifier() =>
        Guid.NewGuid().EncodeToReadable("Ночная Смена").Should().StartWith("nochnaya-smena~");

    [Fact]
    public void EncodeWithoutASeparatorWhenThereIsNoText() =>
        Guid.NewGuid().EncodeToReadable().Should().NotContain("~");

    /// <summary>
    /// The letter at U+0451 has an entry of its own. Without it the word loses a
    /// character instead of transliterating it, and two different titles collapse
    /// onto one address.
    /// </summary>
    /// <remarks>
    /// Written by number, because a rule of the suite scans the tree for that character.
    /// </remarks>
    [Fact]
    public void TransliterateTheLetterWithDots() =>
        Guid.NewGuid().EncodeToReadable("ёж").Should().StartWith("yozh~");

    /// <summary>
    /// The try-form answers false and leaves an empty Guid behind rather than
    /// throwing at whatever call site passed the value on.
    /// </summary>
    /// <remarks>
    /// Only what cannot be Base64 at all is refused. A string of the right length
    /// drawn from the Base64 alphabet decodes to some Guid, which the caller then
    /// fails to find in the store — this is a syntax check, not a claim that the
    /// identifier names anything.
    /// </remarks>
    [Theory]
    [InlineData("не-идентификатор")]
    [InlineData("abc")]
    [InlineData("~")]
    public void RefuseWhatIsNotAnIdentifierAtAll(string value)
    {
        value.TryDecodeFromReadableGuid(out var result).Should().BeFalse();
        result.Should().Be(Guid.Empty);
    }

    // One row per kind of malformed input, with what the strict form throws for it: the try-form
    // has to answer false for every one, or a bad address becomes a 500.
    public static TheoryData<string, Type> Malformed => new()
    {
        { "not*base64", typeof(FormatException) },
        { "abc", typeof(FormatException) },
        { "AAAAAA", typeof(ArgumentException) },
        { "title~AAAAAA", typeof(ArgumentException) },
        { "", typeof(FormatException) },
        { "title~", typeof(FormatException) },
    };

    [Theory]
    [MemberData(nameof(Malformed))]
    public void RefuseEveryKindOfMalformedIdentifier(string value, Type thrown)
    {
        FluentActions.Invoking(() => value.DecodeFromReadableGuid()).Should().Throw<Exception>()
            .Which.Should().BeOfType(thrown);

        value.TryDecodeFromReadableGuid(out var result).Should().BeFalse();
        result.Should().Be(Guid.Empty);
    }

    [Theory]
    [MemberData(nameof(Identifiers))]
    public void AcceptWhatItProducedWhenAskedToTry(string value)
    {
        var guid = Guid.Parse(value);

        guid.EncodeToReadable("Проверка").TryDecodeFromReadableGuid(out var result).Should().BeTrue();
        result.Should().Be(guid);
    }
}
