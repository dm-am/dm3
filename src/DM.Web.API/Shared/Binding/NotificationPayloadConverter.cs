using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DM.Web.API.Shared.Binding;

/// <summary>
/// Writes notification metadata with the keys the bus already uses.
/// </summary>
/// <remarks>
/// The hub pushes the bus JSON verbatim, so the list writes the same camelCase
/// keys; a dictionary key is not touched by PropertyNamingPolicy. Scoped to this
/// property, since a global DictionaryKeyPolicy would rename the keys of every
/// validation error, and on web defaults rather than ApplyApiConventions, since
/// the bus writes an enum as its number.
/// </remarks>
internal class NotificationPayloadConverter : JsonConverter<object>
{
    private static readonly JsonSerializerOptions PayloadOptions = new(JsonSerializerDefaults.Web)
    {
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase
    };

    /// <inheritdoc />
    public override object Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        JsonElement.ParseValue(ref reader);

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(writer, value, value.GetType(), PayloadOptions);
}
