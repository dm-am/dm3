using System.Text.Json;

namespace DM.Web.API.IntegrationTests.Controllers.General;

// Shared by three tests: a body written in place is a body, not its absence.
internal static class ResponseBodySchema
{
    // Never an envelope and never a declared list: those are classes, and the document points at a
    // class through $ref.
    public const string WrittenInPlace = "(schema written in place)";

    /// <summary>
    /// The schema a response body names, <see cref="WrittenInPlace"/> when it
    /// names none, and null when the response carries no body at all.
    /// </summary>
    /// <remarks>The "#/components/schemas/" prefix is dropped.</remarks>
    public static string? NameOf(JsonElement response)
    {
        if (!response.TryGetProperty("content", out var content))
        {
            return null;
        }

        foreach (var mediaType in content.EnumerateObject())
        {
            if (mediaType.Value.TryGetProperty("schema", out var schema) &&
                schema.TryGetProperty("$ref", out var reference) &&
                reference.GetString() is { } id)
            {
                return id.Split('/').Last();
            }
        }

        return WrittenInPlace;
    }

    // Asked only of a body that names nothing.
    public static bool IsArray(JsonElement response)
    {
        if (!response.TryGetProperty("content", out var content))
        {
            return false;
        }

        foreach (var mediaType in content.EnumerateObject())
        {
            if (mediaType.Value.TryGetProperty("schema", out var schema) &&
                schema.TryGetProperty("type", out var type) &&
                type.ValueKind == JsonValueKind.String &&
                type.ValueEquals("array"))
            {
                return true;
            }
        }

        return false;
    }
}
