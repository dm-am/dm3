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
    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        (JsonConverter)Activator.CreateInstance(
            typeof(OptionalConverter<>).MakeGenericType(typeToConvert.GetGenericArguments()[0]),
            BindingFlags.Instance | BindingFlags.Public,
            null, Array.Empty<object>(), null)!;
}

/// <inheritdoc />
internal class OptionalConverter<TValue> : JsonConverter<Optional<TValue>> where TValue : struct
{
    /// <summary>
    /// A JSON null is a value here, not an absent property.
    /// </summary>
    /// <remarks>
    /// Three states survive the wire: missing (leave alone), null (clear), set
    /// (assign). Without HandleNull the serializer would assign null to the
    /// property itself, which the domain reads as "absent".
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

        // By the value's own converter, whatever its token.
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
