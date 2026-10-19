using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using DM.Domain.Core.Extensions;

namespace DM.Web.API.Shared.Binding;

/// <summary>
/// Converter for readable guid fields
/// </summary>
internal class ReadableGuidConverter : JsonConverter<Guid>
{
    /// <inheritdoc />
    public override Guid Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var readerValue = reader.GetString();
        return string.IsNullOrEmpty(readerValue) ? default : Parse(readerValue);
    }

    /// <summary>
    /// Either spelling the site hands out: a plain or a readable identifier
    /// </summary>
    internal static bool TryParse(string value, out Guid guid) =>
        Guid.TryParse(value, out guid) || value.TryDecodeFromReadableGuid(out guid);

    /// <summary>
    /// Read either spelling the site hands out, and refuse anything else
    /// </summary>
    /// <remarks>
    /// The refusal reaches MVC as a model state error and is answered 400, as the
    /// route binder answers the same value, rather than as a request about nobody.
    /// </remarks>
    internal static Guid Parse(string value) =>
        TryParse(value, out var guid) ? guid : throw new JsonException("The value is not an identifier");

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, Guid value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());
}

/// <summary>
/// Converter for readable but nullable fields
/// </summary>
internal class ReadableNullableGuidConverter : JsonConverter<Guid?>
{
    /// <inheritdoc />
    public override Guid? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var readerValue = reader.GetString();
        return string.IsNullOrEmpty(readerValue) ? null : ReadableGuidConverter.Parse(readerValue);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, Guid? value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value?.ToString());
}
