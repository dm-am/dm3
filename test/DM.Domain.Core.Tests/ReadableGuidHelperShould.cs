using System;
using DM.Domain.Core.Extensions;
using FluentAssertions;
using Xunit;

namespace DM.Domain.Core.Tests;

/// <summary>
/// The identifier that carries a title in it and still decodes back to a Guid.
/// </summary>
/// <remarks>
/// Two properties, and the whole point of the helper is that they hold together:
/// the text is there for a person to read, and the Guid is what the address is
/// resolved by. Base64 is not URL-safe, so the encoding swaps three characters
/// and the decoder swaps them back — a change to one side alone leaves addresses
/// that look right and resolve to nothing, or to something else.
///
/// The transliteration table is why the letter at U+0451 is allowed to appear in
/// that one source file: a table missing it turns the letter into nothing.
/// </remarks>
public class ReadableGuidHelperShould
{
    /// <summary>
    /// Guids chosen so their Base64 spells all three characters that have to be
    /// swapped: the pair a URL would take differently, and the padding.
    /// </summary>
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

    /// <summary>
    /// Nothing that has to survive a URL: the three characters Base64 produces
    /// and an address does not want are the ones the encoder replaces.
    /// </summary>
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
    /// Written by number, because the convention keeps that character out of every
    /// text a person reads and a rule of the suite scans the tree for it. The
    /// transliteration table is the one file allowed to carry it; this one states
    /// the same fact without becoming a second exception.
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

    [Theory]
    [MemberData(nameof(Identifiers))]
    public void AcceptWhatItProducedWhenAskedToTry(string value)
    {
        var guid = Guid.Parse(value);

        guid.EncodeToReadable("Проверка").TryDecodeFromReadableGuid(out var result).Should().BeTrue();
        result.Should().Be(guid);
    }
}
