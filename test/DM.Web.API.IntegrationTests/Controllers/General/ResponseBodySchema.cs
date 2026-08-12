using System.Linq;
using System.Text.Json;

namespace DM.Web.API.IntegrationTests.Controllers.General;

/// <summary>
/// How the published document types the body of one response.
/// </summary>
/// <remarks>
/// Three tests in this folder enumerate the same corner of the document, each
/// with a copy of this reading, and all three copies answered null twice: once
/// for a response that carries no body, and once for a body the document
/// describes in place instead of naming - an array, a primitive, a composition.
/// Every caller reads null as "no body", so an operation answering a bare array
/// left the set the test enumerates: not counted among the violations, not held
/// by any list of exemptions, under a class promising that one of the two is
/// always true of it. In ListBoundsShould that is the worst case of all, because
/// a bare array is exactly what an unpaged list looks like.
///
/// The two answers are told apart here, once, so that the distinction cannot be
/// made in one file and forgotten in the next - which is how one reading became
/// three.
/// </remarks>
internal static class ResponseBodySchema
{
    /// <summary>
    /// Stands for a body the document describes in place instead of naming. Never
    /// an envelope and never a declared list: those are classes, and the document
    /// points at a class through $ref.
    /// </summary>
    public const string WrittenInPlace = "(schema written in place)";

    /// <summary>
    /// The schema a response body names, <see cref="WrittenInPlace"/> when it
    /// names none, and null when the response carries no body at all.
    /// </summary>
    /// <remarks>
    /// The name is the id the document registers the class under, with the
    /// "#/components/schemas/" in front of it dropped: what the callers ask is
    /// whether it is an envelope, and the prefix is the same on every answer.
    /// </remarks>
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

    /// <summary>Whether the body is an array, written out in place.</summary>
    /// <remarks>
    /// Asked only of a body that names nothing: an array of resources is a list
    /// with no envelope around it, and to the caller that is still a list.
    /// </remarks>
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
