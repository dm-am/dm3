using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Exceptions;

namespace DM.Domain.Core.Content;

/// <summary>
/// A character that a [private] block may name, paired with the user who owns
/// it. Supplied by the save path from the roster of the room the post is going
/// into.
/// </summary>
/// <param name="CharacterId">
/// The character itself. A name is not an identity — it can be given up and
/// taken by somebody else — and this is what the snapshot compares by.
/// </param>
/// <param name="CharacterName">Character name as it appears to the author.</param>
/// <param name="OwnerUserId">User the character belongs to.</param>
public readonly record struct PrivateAddressee(
    Guid CharacterId, string CharacterName, Guid OwnerUserId);

/// <summary>
/// A stored snapshot, in the two shapes its readers need: who may see each
/// block, and who to name as its recipients.
/// </summary>
/// <param name="OwnerUserIdsByAttribute">
/// Owner user ids per tag attribute value — what the visibility filter matches
/// a reader against.
/// </param>
/// <param name="AddresseeNamesByAttribute">
/// Names of the frozen addressees per tag attribute value, for the recipients
/// line. A key is absent when the snapshot does not name every addressee under
/// it — an old snapshot does not name any — and the caller then has nothing to
/// print but the author's own text.
/// </param>
public sealed record PrivateAddresseeSnapshotView(
    IReadOnlyDictionary<string, IReadOnlySet<Guid>> OwnerUserIdsByAttribute,
    IReadOnlyDictionary<string, IReadOnlyList<string>> AddresseeNamesByAttribute);

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
/// Shape:
/// <c>{ "AttributeValue": [ { "character": "guid", "name": "...", "owner": "guid" }, ... ] }</c>.
/// The key is the tag attribute exactly as
/// <see cref="PrivateBlockMarkup.ReadAddresseeAttributes"/> reports it, which is
/// what the parser hands the visibility filter.
///
/// An earlier shape wrote the value as a bare list of owner ids,
/// <c>{ "AttributeValue": ["guid", ...] }</c>, and those rows are in the
/// database. They are still read: an element that is a string is an owner whose
/// character nobody recorded. Such an entry names no addressee and takes part in
/// no comparison — see <see cref="Build(string,string,IEnumerable{PrivateAddressee})"/>,
/// where unknown means the frozen entry wins and no refusal is raised. A row
/// written by the old code therefore keeps working exactly as it did.
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

    private const string CharacterField = "character";
    private const string NameField = "name";
    private const string OwnerField = "owner";

    /// <summary>
    /// What to tell the author when the name they wrote no longer belongs to the
    /// character this post froze it to.
    /// </summary>
    /// <remarks>
    /// The refusal is the point of the check, so it has to say which name is at
    /// fault and what to do about it. What it deliberately does not say is who
    /// holds the name now, or who held it before: the author may address a
    /// character they are not entitled to be told anything else about, and a
    /// refusal is not a place to hand out roster facts.
    /// </remarks>
    /// <param name="attribute">The tag attribute the author wrote.</param>
    public static string DescribeRenameRefusal(string attribute) =>
        $"Имя \"{attribute}\" в скрытом блоке сейчас принадлежит другому персонажу, " +
        "не тому, кому этот блок был адресован при сохранении. Кому показывать блок, " +
        "по имени уже не определить, и сохранение отправило бы текст не тому читателю. " +
        "Напишите нынешнее имя адресата или уберите блок с этим именем.";

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
    /// <exception cref="HttpException">
    /// The text names somebody the post has already frozen the same name to, and
    /// the two are different characters. See the remarks.
    /// </exception>
    /// <remarks>
    /// A block that was resolved before keeps the ids it got then — re-resolving
    /// it against today's roster is exactly what addressee-forever forbids, and
    /// would quietly revoke a player whose character has since left the room.
    /// Blocks the edit introduced resolve now; keys whose block the edit removed
    /// are dropped, since nothing looks them up any more.
    ///
    /// The snapshot is keyed by the name the author wrote, and a name outlives
    /// nothing: a character can be renamed and its old name taken by another. So
    /// a key that already exists can address one character in the block it was
    /// frozen for and a different one in a block written today, and the two are
    /// indistinguishable — a block carries no mark saying which save wrote it.
    /// Where the two answers differ the save is refused rather than guessed at:
    /// the question is who reads a secret, and the author is the one who knows.
    ///
    /// Three neighbouring cases are not that and go through:
    /// the name still resolves to the character it was frozen to; the name
    /// resolves to nobody today, which is the ordinary shape of addressee-forever
    /// and keeps the frozen reader; and a name the frozen snapshot has never seen,
    /// which is a fresh key resolved against today's roster. An old snapshot,
    /// which recorded owners without their characters, cannot be compared at all
    /// and is treated as the first of those.
    ///
    /// The names of frozen addressees are refreshed from the roster by character
    /// id whenever the character is still in the room. That moves no access — the
    /// reader is the frozen owner either way — and it is what keeps the recipients
    /// line naming the addressee as they are called now.
    /// </remarks>
    public static string Build(
        string? rawBbCode,
        string? previousSnapshotJson,
        IEnumerable<PrivateAddressee> addressees)
    {
        var attributes = PrivateBlockMarkup.ReadAddresseeAttributes(rawBbCode);
        if (attributes.Count == 0) return Empty;

        var candidates = addressees as IReadOnlyCollection<PrivateAddressee> ?? addressees.ToList();
        var frozen = ReadEntries(previousSnapshotJson);
        var byName = IndexByName(candidates);
        var nameByCharacterId = IndexNamesByCharacterId(candidates);
        var resolved = new Dictionary<string, List<Entry>>(StringComparer.Ordinal);

        foreach (var attribute in attributes)
        {
            var today = Resolve(attribute, byName);
            List<Entry> entries;

            if (frozen.TryGetValue(attribute, out var already))
            {
                ThrowIfNameMoved(attribute, already, today);
                entries = Refresh(already, nameByCharacterId);
            }
            else
            {
                entries = today
                    .Select(a => new Entry(a.CharacterId, a.CharacterName, a.OwnerUserId))
                    .ToList();
            }

            if (entries.Count == 0) continue;

            // Set and roster order are not defined, and a snapshot that reshuffles
            // itself on every save is a diff nobody can read.
            entries.Sort(CompareEntries);
            resolved[attribute] = entries;
        }

        return resolved.Count == 0 ? Empty : Serialize(resolved);
    }

    /// <summary>
    /// Read a stored snapshot. Anything malformed reads as empty, which denies
    /// through this rule rather than granting through it.
    /// </summary>
    /// <param name="json">Stored snapshot JSON.</param>
    /// <returns>Owner user ids per tag attribute value.</returns>
    public static IReadOnlyDictionary<string, IReadOnlySet<Guid>> Parse(string? json) =>
        Read(json).OwnerUserIdsByAttribute;

    /// <summary>
    /// Read a stored snapshot in both the shapes the render path needs.
    /// </summary>
    /// <param name="json">Stored snapshot JSON.</param>
    public static PrivateAddresseeSnapshotView Read(string? json)
    {
        var owners = new Dictionary<string, IReadOnlySet<Guid>>(StringComparer.Ordinal);
        var names = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

        foreach (var (attribute, entries) in ReadEntries(json))
        {
            var attributeOwners = new HashSet<Guid>();
            var attributeNames = new List<string>();
            var everyoneNamed = true;

            foreach (var entry in entries)
            {
                attributeOwners.Add(entry.OwnerUserId);
                if (string.IsNullOrEmpty(entry.CharacterName))
                {
                    everyoneNamed = false;
                    continue;
                }

                if (!attributeNames.Contains(entry.CharacterName))
                    attributeNames.Add(entry.CharacterName);
            }

            if (attributeOwners.Count == 0) continue;
            owners[attribute] = attributeOwners;

            // Naming some of the addressees and silently leaving out the rest is
            // worse than naming none: the reader reads the line as the whole
            // list. An old snapshot names nobody and lands here every time.
            if (everyoneNamed && attributeNames.Count > 0) names[attribute] = attributeNames;
        }

        return new PrivateAddresseeSnapshotView(owners, names);
    }

    /// <summary>One frozen addressee, as the stored snapshot holds it.</summary>
    /// <param name="CharacterId">
    /// Null when the snapshot predates the field. Unknown, not absent: the
    /// entry stands, and every comparison that needs the character stands down.
    /// </param>
    /// <param name="CharacterName">Null for the same reason.</param>
    /// <param name="OwnerUserId">The reader the block was frozen to.</param>
    private readonly record struct Entry(Guid? CharacterId, string? CharacterName, Guid OwnerUserId);

    /// <summary>
    /// Refuse the save when the written name resolves today to a character this
    /// post did not freeze it to.
    /// </summary>
    /// <remarks>
    /// Only an addition is a refusal. A frozen character the name no longer
    /// reaches — renamed, or gone from the room — is addressee-forever working
    /// as specified, and nothing about it is in doubt.
    /// </remarks>
    private static void ThrowIfNameMoved(
        string attribute,
        IReadOnlyList<Entry> frozen,
        IReadOnlyList<PrivateAddressee> today)
    {
        if (today.Count == 0) return;

        var frozenCharacters = new HashSet<Guid>();
        foreach (var entry in frozen)
        {
            // An entry from an old snapshot carries no character, so the post
            // cannot say which one the name was frozen to and there is nothing
            // to compare today's answer against. Reading that as "a different
            // character" would refuse every edit of every post written before
            // the field existed.
            if (entry.CharacterId is not { } characterId) return;
            frozenCharacters.Add(characterId);
        }

        if (today.Any(a => !frozenCharacters.Contains(a.CharacterId)))
            throw new HttpException(HttpStatusCode.BadRequest, DescribeRenameRefusal(attribute));
    }

    /// <summary>
    /// Frozen entries with the names their characters go by now.
    /// </summary>
    private static List<Entry> Refresh(
        IReadOnlyList<Entry> frozen,
        IReadOnlyDictionary<Guid, string> nameByCharacterId) =>
        frozen
            .Select(entry => entry.CharacterId is { } characterId &&
                             nameByCharacterId.TryGetValue(characterId, out var current)
                ? entry with { CharacterName = current }
                : entry)
            .ToList();

    private static int CompareEntries(Entry left, Entry right)
    {
        var byOwner = left.OwnerUserId.CompareTo(right.OwnerUserId);
        return byOwner != 0
            ? byOwner
            : (left.CharacterId ?? Guid.Empty).CompareTo(right.CharacterId ?? Guid.Empty);
    }

    private static string Serialize(Dictionary<string, List<Entry>> resolved)
    {
        using var stream = new System.IO.MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            foreach (var (attribute, entries) in resolved)
            {
                writer.WriteStartArray(attribute);
                foreach (var entry in entries)
                {
                    writer.WriteStartObject();
                    if (entry.CharacterId is { } characterId)
                        writer.WriteString(CharacterField, characterId);
                    if (!string.IsNullOrEmpty(entry.CharacterName))
                        writer.WriteString(NameField, entry.CharacterName);
                    writer.WriteString(OwnerField, entry.OwnerUserId);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
            }

            writer.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>
    /// Stored entries per tag attribute value, in both stored shapes. Anything
    /// malformed reads as empty.
    /// </summary>
    private static Dictionary<string, List<Entry>> ReadEntries(string? json)
    {
        var result = new Dictionary<string, List<Entry>>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(json) || json == Empty) return result;

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object) return result;

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.Array) continue;

                var entries = new List<Entry>();
                foreach (var item in property.Value.EnumerateArray())
                {
                    if (ReadEntry(item) is { } entry && !entries.Contains(entry))
                        entries.Add(entry);
                }

                if (entries.Count > 0) result[property.Name] = entries;
            }

            return result;
        }
        catch (JsonException)
        {
            return new Dictionary<string, List<Entry>>(StringComparer.Ordinal);
        }
    }

    /// <summary>
    /// One stored addressee, whichever shape it was written in: an object with
    /// the character beside the owner, or the bare owner id the earlier format
    /// wrote.
    /// </summary>
    private static Entry? ReadEntry(JsonElement item)
    {
        switch (item.ValueKind)
        {
            case JsonValueKind.String:
                return ReadOwner(item) is { } legacyOwner
                    ? new Entry(null, null, legacyOwner)
                    : null;

            case JsonValueKind.Object:
                if (!item.TryGetProperty(OwnerField, out var owner) ||
                    ReadOwner(owner) is not { } ownerUserId)
                    return null;

                var characterId = item.TryGetProperty(CharacterField, out var character) &&
                                  character.ValueKind == JsonValueKind.String &&
                                  Guid.TryParse(character.GetString(), out var parsed)
                    ? parsed
                    : (Guid?)null;

                var name = item.TryGetProperty(NameField, out var stored) &&
                           stored.ValueKind == JsonValueKind.String
                    ? stored.GetString()
                    : null;

                return new Entry(characterId, string.IsNullOrEmpty(name) ? null : name, ownerUserId);

            default:
                return null;
        }
    }

    /// <summary>
    /// An owner id, or null for anything that is not one.
    /// </summary>
    /// <remarks>
    /// The empty id is dropped rather than read as an owner: it is the id an
    /// anonymous reader carries, so a snapshot holding one would open its block
    /// to everybody who is not signed in. The save path never writes it; a
    /// hand-written row or an import can (see AnonymousIdentity).
    /// </remarks>
    private static Guid? ReadOwner(JsonElement element) =>
        element.ValueKind == JsonValueKind.String &&
        Guid.TryParse(element.GetString(), out var ownerUserId) &&
        !AnonymousIdentity.Is(ownerUserId)
            ? ownerUserId
            : null;

    /// <summary>
    /// Candidates keyed by name. Case- and padding-insensitive: the author types
    /// the name, they do not pick it from a list. A name shared by two characters
    /// resolves to both — the text names a character, and nothing in it
    /// distinguishes namesakes.
    /// </summary>
    private static Dictionary<string, List<PrivateAddressee>> IndexByName(
        IEnumerable<PrivateAddressee> addressees)
    {
        var byName = new Dictionary<string, List<PrivateAddressee>>(StringComparer.OrdinalIgnoreCase);
        foreach (var addressee in addressees)
        {
            var name = addressee.CharacterName?.Trim();
            if (string.IsNullOrEmpty(name) || AnonymousIdentity.Is(addressee.OwnerUserId)) continue;
            if (!byName.TryGetValue(name, out var found))
                byName[name] = found = new List<PrivateAddressee>();
            if (!found.Contains(addressee)) found.Add(addressee);
        }

        return byName;
    }

    /// <summary>Current name of every candidate character, by its id.</summary>
    private static Dictionary<Guid, string> IndexNamesByCharacterId(
        IEnumerable<PrivateAddressee> addressees)
    {
        var byId = new Dictionary<Guid, string>();
        foreach (var addressee in addressees)
        {
            var name = addressee.CharacterName?.Trim();
            if (string.IsNullOrEmpty(name) || addressee.CharacterId == Guid.Empty) continue;
            byId[addressee.CharacterId] = name;
        }

        return byId;
    }

    /// <summary>
    /// Characters named by one tag attribute. The value is a list of names — the
    /// block header renders it as "Получатели" — so it splits on commas; a name
    /// that itself contains one resolves to nobody, which shows the block to
    /// nobody rather than to the wrong reader.
    /// </summary>
    private static List<PrivateAddressee> Resolve(
        string attribute,
        IReadOnlyDictionary<string, List<PrivateAddressee>> byName)
    {
        var found = new List<PrivateAddressee>();
        if (byName.Count == 0) return found;

        foreach (var part in attribute.Split(','))
        {
            var name = part.Trim();
            if (name.Length == 0) continue;
            if (!byName.TryGetValue(name, out var matches)) continue;
            foreach (var match in matches)
                if (!found.Contains(match))
                    found.Add(match);
        }

        return found;
    }
}
