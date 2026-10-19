using System;
using System.Text.Json;
using DM.Domain.Core.Dto;
using DM.Web.API.Shared.Binding;
using AwesomeAssertions;
using Xunit;

namespace DM.Web.API.Tests.Shared;

/// <summary>
/// Optional carries three states across the wire, and PATCH needs all three.
/// </summary>
/// <remarks>Optional is a class, so with the default HandleNull the serialiser skips Read on a null.</remarks>
public class OptionalConverterShould
{
    private class Target
    {
        public Optional<Guid>? PreviousRoomId { get; set; }

        public Optional<int>? Position { get; set; }
    }

    private static readonly JsonSerializerOptions Options = BuildOptions();

    private static JsonSerializerOptions BuildOptions()
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        options.Converters.Add(new OptionalConverterFactory());
        return options;
    }

    [Fact]
    public void ReadAnAbsentPropertyAsNoChange()
    {
        var target = JsonSerializer.Deserialize<Target>("""{}""", Options)!;

        target.PreviousRoomId.Should().BeNull("an absent property asks for nothing");
    }

    [Fact]
    public void ReadAnExplicitNullAsClear()
    {
        var target = JsonSerializer.Deserialize<Target>(
            """{"previousRoomId":null}""", Options)!;

        target.PreviousRoomId.Should().NotBeNull("an explicit null is a value, not an absence");
        target.PreviousRoomId!.Value.Should().BeNull();
    }

    [Fact]
    public void ReadAValueAsAssign()
    {
        var id = Guid.NewGuid();

        var target = JsonSerializer.Deserialize<Target>(
            $$"""{"previousRoomId":"{{id}}"}""", Options)!;

        target.PreviousRoomId!.Value.Should().Be(id);
    }

    [Fact]
    public void ReadANonStringValue()
    {
        // A value read as a string first throws, and the throw answers 500.
        var target = JsonSerializer.Deserialize<Target>("""{"position":7}""", Options)!;

        target.Position!.Value.Should().Be(7);
    }
}
