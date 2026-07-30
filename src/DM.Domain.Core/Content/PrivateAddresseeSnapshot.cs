using System;
using System.Collections.Generic;
using System.Text.Json;

namespace DM.Domain.Core.Content;

/// <summary>
/// A character that a [private] block may name, paired with the user who owns
/// it. Supplied by the save path from the roster of the room the post is going
/// into.
/// </summary>
/// <param name="CharacterName">Character name as it appears to the author.</param>
/// <param name="OwnerUserId">User the character belongs to.</param>
public readonly record struct PrivateAddressee(string CharacterName, Guid OwnerUserId);

/// <summary>
/// Writer and reader of the per-post [private] addressee snapshot.
///
/// The visibility rule is addressee-forever (BBCODE_RENDERING.md): the owner of
/// an addressed character keeps seeing the block even after the character leaves
/// the game, so the names in the text are resolved to user ids once, at save
/// time, and frozen. Both ends of that contract live here — the JSON shape and
/// the key the render path looks blocks up by are one definition, not two.
/// </summary>
/// <remarks>
/// Shape: <c>{ "AttributeValue": ["guid", ...], ... }</c>. The key is the tag
/// attribute exactly as <see cref="PrivateBlockMarkup.ReadAddresseeAttributes"/>
/// reports it, which is what the parser hands the visibility filter.
///
/// Resolution can only ever name someone who already reads the room the post is
/// in, because the candidate list is that room's roster: a snapshot grants no
/// access that room membership did not already grant. An unresolvable name
/// yields no entry, and an absent entry means nobody qualifies through this
/// rule — the failure direction is closed, not open.
/// </remarks>
public static class PrivateAddresseeSnapshot
{
    /// <summary>Snapshot of a post that addresses nobody.</summary>
    public const string Empty = "{}";

    /// <summary>
    /// Resolve every [private=...] block of freshly written text against the
    /// given candidates.
    /// </summary>
    /// <param name="rawBbCode">Post text as stored.</param>
    /// <param name="addressees">Characters the author could have named.</param>
    /// <returns>Snapshot JSON, <see cref="Empty"/> when nothing resolved.</returns>
    public static string Build(string? rawBbCode, IEnumerable<PrivateAddressee> addressees) =>
        Build(rawBbCode, null, addressees);

    /// <summary>
    /// Resolve every [private=...] block of edited text, keeping what an earlier
    /// save already resolved.
    /// </summary>
    /// <param name="rawBbCode">Post text as stored after the edit.</param>
    /// <param name="previousSnapshotJson">Snapshot the post carried before it.</param>
    /// <param name="addressees">Characters the author could have named.</param>
    /// <returns>Snapshot JSON, <see cref="Empty"/> when nothing resolved.</returns>
    /// <remarks>
    /// A block that was resolved before keeps the ids it got then — re-resolving
    /// it against today's roster is exactly what addressee-forever forbids, and
    /// would quietly revoke a player whose character has since left the room.
    /// Blocks the edit introduced resolve now; keys whose block the edit removed
    /// are dropped, since nothing looks them up any more.
    /// </remarks>
    public static string Build(
        string? rawBbCode,
        string? previousSnapshotJson,
        IEnumerable<PrivateAddressee> addressees)
    {
        var attributes = PrivateBlockMarkup.ReadAddresseeAttributes(rawBbCode);
        if (attributes.Count == 0) return Empty;

        var frozen = Parse(previousSnapshotJson);
        var byName = IndexByName(addressees);
        var resolved = new Dictionary<string, Guid[]>(StringComparer.Ordinal);

        foreach (var attribute in attributes)
        {
            var owners = frozen.TryGetValue(attribute, out var already)
                ? already
                : Resolve(attribute, byName);
            if (owners.Count == 0) continue;

            var ordered = new Guid[owners.Count];
            var index = 0;
            foreach (var owner in owners) ordered[index++] = owner;
            // Set order is not defined, and a snapshot that reshuffles itself on
            // every save is a diff nobody can read.
            Array.Sort(ordered);
            resolved[attribute] = ordered;
        }

        return resolved.Count == 0 ? Empty : JsonSerializer.Serialize(resolved);
    }

    /// <summary>
    /// Read a stored snapshot. Anything malformed reads as empty, which denies
    /// through this rule rather than granting through it.
    /// </summary>
    /// <param name="json">Stored snapshot JSON.</param>
    /// <returns>Owner user ids per tag attribute value.</returns>
    public static IReadOnlyDictionary<string, IReadOnlySet<Guid>> Parse(string? json)
    {
        var result = new Dictionary<string, IReadOnlySet<Guid>>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(json) || json == Empty) return result;

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object) return result;

            foreach (var entry in document.RootElement.EnumerateObject())
            {
                if (entry.Value.ValueKind != JsonValueKind.Array) continue;
                var owners = new HashSet<Guid>();
                foreach (var item in entry.Value.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String &&
                        Guid.TryParse(item.GetString(), out var ownerUserId))
                        owners.Add(ownerUserId);
                }

                if (owners.Count > 0) result[entry.Name] = owners;
            }

            return result;
        }
        catch (JsonException)
        {
            return new Dictionary<string, IReadOnlySet<Guid>>(StringComparer.Ordinal);
        }
    }

    /// <summary>
    /// Owner ids of every candidate, keyed by name. Case- and padding-
    /// insensitive: the author types the name, they do not pick it from a list.
    /// A name shared by two characters resolves to both owners — the text names
    /// a character, and nothing in it distinguishes namesakes.
    /// </summary>
    private static Dictionary<string, HashSet<Guid>> IndexByName(
        IEnumerable<PrivateAddressee> addressees)
    {
        var byName = new Dictionary<string, HashSet<Guid>>(StringComparer.OrdinalIgnoreCase);
        foreach (var addressee in addressees)
        {
            var name = addressee.CharacterName?.Trim();
            if (string.IsNullOrEmpty(name) || addressee.OwnerUserId == Guid.Empty) continue;
            if (!byName.TryGetValue(name, out var owners))
                byName[name] = owners = new HashSet<Guid>();
            owners.Add(addressee.OwnerUserId);
        }

        return byName;
    }

    /// <summary>
    /// Owners named by one tag attribute. The value is a list of names — the
    /// block header renders it as "Получатели" — so it splits on commas; a name
    /// that itself contains one resolves to nobody, which shows the block to
    /// nobody rather than to the wrong reader.
    /// </summary>
    private static IReadOnlyCollection<Guid> Resolve(
        string attribute,
        IReadOnlyDictionary<string, HashSet<Guid>> byName)
    {
        if (byName.Count == 0) return Array.Empty<Guid>();

        var owners = new HashSet<Guid>();
        foreach (var part in attribute.Split(','))
        {
            var name = part.Trim();
            if (name.Length == 0) continue;
            if (byName.TryGetValue(name, out var found)) owners.UnionWith(found);
        }

        return owners;
    }
}
