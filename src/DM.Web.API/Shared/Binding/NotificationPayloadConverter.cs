using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DM.Web.API.Shared.Binding;

/// <summary>
/// Writes notification metadata with the keys the bus already uses.
/// </summary>
/// <remarks>
/// One notification reaches one open tab twice: pushed over the hub and read
/// back from the list. The pushed copy is the JSON the dispatcher published,
/// which System.Text.Json wrote in camelCase and which arrives here as a
/// JsonElement. The stored copy comes out of Mongo as a dictionary whose keys
/// are the CLR member names of the metadata object, and a dictionary key is not
/// touched by PropertyNamingPolicy. So one field left as gameTitle over the
/// socket and as GameTitle over REST, and the reader of the list found neither
/// a title nor a link because it was written against the other spelling.
///
/// Scoped to this one property by attribute rather than fixed on the serializer
/// with DictionaryKeyPolicy: that switch is global and would rename the keys of
/// every validation error body the API answers with, which is a different
/// contract and a different decision.
///
/// The nested options are deliberately the plain web defaults and not
/// ApplyApiConventions. The other half of this wire is bus JSON copied through
/// verbatim, and the bus writes an enum as its number; taking the API's string
/// enums here would open a new split in place of the one being closed.
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
