using System;
using System.Text.Json;
using DM.Domain.Core.Extensions;
using DM.Web.API.Shared.Binding;
using AwesomeAssertions;
using Xunit;

namespace DM.Web.API.Tests.Shared.Binding;

/// <summary>
/// An identifier in a request body reads from either spelling the site hands
/// out and is written back in the plain one.
/// </summary>
/// <remarks>
/// The two converters are inserted first into the options every body goes
/// through, so a field typed Guid in any request reads the readable form too.
/// They differ from the route binder in one way that matters: a value they
/// cannot read becomes the empty identifier, or nothing for an optional one,
/// rather than a refusal. It is held here so that a change to it is made on
/// purpose.
/// </remarks>
public class ReadableGuidConverterShould
{
    private static readonly Guid Identifier = Guid.Parse("8b1e4c2a-37d5-4f61-9a0b-c3d4e5f60718");

    private static readonly JsonSerializerOptions Options = BuildOptions();

    private static JsonSerializerOptions BuildOptions()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Insert(0, new ReadableGuidConverter());
        options.Converters.Insert(0, new ReadableNullableGuidConverter());
        return options;
    }

    public static TheoryData<string> Spellings => new()
    {
        Identifier.ToString(),
        Identifier.ToString("N"),
        Identifier.EncodeToReadable(),
        Identifier.EncodeToReadable("Тема о драконах"),
    };

    [Theory]
    [MemberData(nameof(Spellings))]
    public void ReadEverySpellingTheSiteHandsOut(string spelling)
    {
        Read<Guid>(spelling).Should().Be(Identifier);
        Read<Guid?>(spelling).Should().Be(Identifier);
    }

    [Fact]
    public void ReadAValueThatIsNeitherSpellingAsTheEmptyIdentifier() =>
        Read<Guid>("not-a-guid!").Should().Be(Guid.Empty,
            "the converter answers what it cannot read with the default rather than a refusal");

    [Fact]
    public void ReadAValueThatIsNeitherSpellingAsNoIdentifierWhereOneIsOptional() =>
        Read<Guid?>("not-a-guid!").Should().BeNull();

    [Fact]
    public void ReadAnEmptyStringAsNoIdentifier()
    {
        Read<Guid>(string.Empty).Should().Be(Guid.Empty);
        Read<Guid?>(string.Empty).Should().BeNull();
    }

    [Fact]
    public void ReadNullAsNoIdentifier()
    {
        JsonSerializer.Deserialize<Guid>("null", Options).Should().Be(Guid.Empty);
        JsonSerializer.Deserialize<Guid?>("null", Options).Should().BeNull();
    }

    [Fact]
    public void WriteThePlainSpelling()
    {
        JsonSerializer.Serialize(Identifier, Options).Should().Be($"\"{Identifier}\"");
        JsonSerializer.Serialize((Guid?)Identifier, Options).Should().Be($"\"{Identifier}\"");
        JsonSerializer.Serialize((Guid?)null, Options).Should().Be("null");
    }

    private static T Read<T>(string value) =>
        JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value), Options)!;
}
