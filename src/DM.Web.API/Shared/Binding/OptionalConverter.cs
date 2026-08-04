using System;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using DM.Domain.Core.Dto;

namespace DM.Web.API.Shared.Binding;

/// <inheritdoc />
internal class OptionalConverterFactory : JsonConverterFactory
{
    /// <inheritdoc />
    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(Optional<>);

    /// <inheritdoc />
    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var optionalType = typeToConvert.GetGenericArguments()[0];
        var converter = (JsonConverter)Activator.CreateInstance(
            typeof(OptionalConverter<>).MakeGenericType(optionalType),
            BindingFlags.Instance | BindingFlags.Public,
            null, Array.Empty<object>(), null)!;
        return converter;
    }
}

/// <inheritdoc />
internal class OptionalConverter<TValue> : JsonConverter<Optional<TValue>> where TValue : struct
{
    /// <summary>
    /// A JSON null is a value here, not an absent property.
    /// </summary>
    /// <remarks>
    /// Three states have to survive the wire: property missing (leave alone),
    /// property null (clear it), property set (assign it). Optional is a class,
    /// so with the default HandleNull the serialiser never called Read on a null
    /// and assigned null to the property itself — which the domain reads as
    /// "absent". Nothing could be cleared through PATCH: a room could not be
    /// moved to the head of the chain, because {"previousRoomId": null} meant
    /// "do not reorder".
    /// </remarks>
    public override bool HandleNull => true;

    /// <inheritdoc />
    public override Optional<TValue>? Read(ref Utf8JsonReader reader, Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return Optional<TValue>.WithValue(null);
        }

        // Read by the value's own converter, whatever its token. Taking a string
        // first worked only because every Optional in the contract happens to
        // wrap a Guid: the first Optional<int> or Optional<bool> would have
        // thrown on a number or a boolean and left the caller with a 500.
        var value = JsonSerializer.Deserialize<TValue>(ref reader, options);
        return Optional<TValue>.WithValue(value);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, Optional<TValue> value, JsonSerializerOptions options)
    {
        var resultValue = value.Value;
        JsonSerializer.Serialize(writer, resultValue, options);
    }
}
