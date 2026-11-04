using System.Net;
using System.Text.Json;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Exceptions;

namespace DM.Domain.Core.Content;

/// <summary>
/// A character that a [private] block may name, paired with the user who owns
/// it. Supplied by the save path from the roster of the room the post is going
/// into, or read back from the mark of a file that froze it.
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
/// the block carries. A key is absent when the snapshot does not name every
/// addressee under it — an old snapshot does not name any — and the caller then
/// has nothing to name but the author's own text.
/// </param>
public sealed record PrivateAddresseeSnapshotView(
    IReadOnlyDictionary<string, IReadOnlySet<Guid>> OwnerUserIdsByAttribute,
    IReadOnlyDictionary<string, IReadOnlyList<string>> AddresseeNamesByAttribute);

/// <summary>
/// The addressees chosen for one file of a post, in the shapes its readers need.
/// </summary>
/// <param name="Characters">
/// The characters chosen, as the choice froze them: the checkboxes of the author, one gone from the
/// room among them, and what a new mark may keep. An entry stored without its character or its name
/// has no box to stand in.
/// </param>
/// <param name="OwnerUserIds">Their owners frozen at the choice: who reads the file.</param>
/// <param name="Names">Their names, for the recipients line.</param>
public sealed record PrivateAddresseeList(
    IReadOnlyList<PrivateAddressee> Characters,
    IReadOnlySet<Guid> OwnerUserIds,
    IReadOnlyList<string> Names)
{
    /// <summary>A file marked for nobody: the author and the leads read it.</summary>
    public static PrivateAddresseeList Nobody { get; } = new([], new HashSet<Guid>(), []);

    /// <summary>Whether the file is addressed to any character, which the overrides of the room ask.</summary>
    public bool NamesAddressees => OwnerUserIds.Count > 0;
}

/// <summary>
/// Writer and reader of the per-post [private] addressee snapshot.
///
/// The visibility rule is addressee-forever (BBCODE_RENDERING.md, "[private]",
/// condition 2): the owner of an addressed character keeps seeing the block even
/// after the character leaves the game, so the names in the text are resolved
/// to user ids once, at save time, and frozen. Both ends of that contract live
/// here — the JSON shape and the key the render path looks blocks up by are one
/// definition, not two.
/// </summary>
/// <remarks>
/// Shape:
/// <c>{ "AttributeValue": [ { "character": "guid", "name": "...", "owner": "guid" }, ... ] }</c>.
/// The key is the tag attribute exactly as
/// <see cref="PrivateBlockMarkup.ReadAddresseeAttributes"/> reports it, which is
/// what the parser hands the visibility filter.
///
/// The candidates are the room's roster, so a snapshot grants no access room
/// membership did not, and an unresolvable name yields no entry (closed, not open).
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
    /// Names the name at fault, never who holds it now or held it before: a refusal
    /// is not a place to hand out roster facts.
    /// </remarks>
    /// <param name="attribute">The tag attribute the author wrote.</param>
    public static string DescribeRenameRefusal(string attribute) =>
        $"Имя \"{attribute}\" в скрытом блоке теперь у другого персонажа. " +
        "Напишите нынешнее имя адресата или уберите блок.";

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
    /// A block resolved before keeps its ids (addressee-forever), blocks the edit
    /// introduced resolve now, keys of removed blocks are dropped. A frozen name that
    /// resolves today to a different character is refused rather than guessed at.
    /// Not refused: the name still resolves to the frozen character, it resolves to
    /// nobody, or it is a key the snapshot has never seen; an entry without a
    /// character cannot be compared and counts as the first. Frozen names are
    /// refreshed by character id, which moves no access.
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
    /// Read a stored snapshot in both the shapes the render path needs. Anything
    /// malformed reads as empty, which denies through this rule rather than
    /// granting through it.
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


    /// <summary>
    /// The addressees of one file, frozen from the characters the author chose.
    /// </summary>
    /// <remarks>
    /// Shape: the array a block keeps under its attribute,
    /// <c>[ { "character": "guid", "name": "...", "owner": "guid" }, ... ]</c>.
    /// The candidates are the roster of the room, as for a block, and what the mark
    /// being changed already holds, so the choice grants nothing the room or the
    /// mark did not; a character outside them is refused rather than dropped, since
    /// the author picked it from a list and dropping it would hide the file from
    /// somebody he meant it for.
    /// </remarks>
    /// <param name="characterIds">The characters chosen; none leaves the file to the author and the leads.</param>
    /// <param name="addressees">Characters the author could have chosen; the first of a character is the one frozen.</param>
    /// <exception cref="HttpException">A character chosen is not among the candidates.</exception>
    public static string BuildList(IEnumerable<Guid> characterIds, IEnumerable<PrivateAddressee> addressees)
    {
        var byCharacter = new Dictionary<Guid, PrivateAddressee>();
        foreach (var addressee in addressees)
        {
            if (addressee.CharacterId != Guid.Empty && !AnonymousIdentity.Is(addressee.OwnerUserId))
                byCharacter.TryAdd(addressee.CharacterId, addressee);
        }

        var entries = new List<Entry>();
        foreach (var characterId in characterIds.Distinct())
        {
            if (!byCharacter.TryGetValue(characterId, out var addressee))
                throw new HttpException(HttpStatusCode.BadRequest, RefusalMessage.CharacterNotInRoom);
            entries.Add(new Entry(addressee.CharacterId, addressee.CharacterName?.Trim(), addressee.OwnerUserId));
        }

        entries.Sort(CompareEntries);
        using var stream = new System.IO.MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            WriteEntries(writer, entries);
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>
    /// The addressees of one file, or null for a file every reader of the post reads.
    /// </summary>
    /// <remarks>
    /// Null is the only spelling of "everybody": anything else that is not a list
    /// of addressees reads as a file marked for nobody, which closes it rather than
    /// opening it.
    /// </remarks>
    /// <param name="json">The stored mark of the file.</param>
    public static PrivateAddresseeList? ReadList(string? json)
    {
        if (json == null) return null;

        var entries = new List<Entry>();
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array) return PrivateAddresseeList.Nobody;

            foreach (var item in document.RootElement.EnumerateArray())
            {
                if (ReadEntry(item) is { } entry && !entries.Contains(entry)) entries.Add(entry);
            }
        }
        catch (JsonException)
        {
            return PrivateAddresseeList.Nobody;
        }

        return new PrivateAddresseeList(
            entries.Where(e => e.CharacterId.HasValue && !string.IsNullOrEmpty(e.CharacterName))
                .Select(e => new PrivateAddressee(e.CharacterId!.Value, e.CharacterName!, e.OwnerUserId))
                .DistinctBy(a => a.CharacterId).ToList(),
            entries.Select(e => e.OwnerUserId).ToHashSet(),
            entries.Where(e => !string.IsNullOrEmpty(e.CharacterName)).Select(e => e.CharacterName!)
                .Distinct().ToList());
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
            // An entry without a character has nothing to compare today's answer against.
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
                writer.WritePropertyName(attribute);
                WriteEntries(writer, entries);
            }

            writer.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>The array of addressees, the one shape a block and a file keep them in.</summary>
    private static void WriteEntries(Utf8JsonWriter writer, IEnumerable<Entry> entries)
    {
        writer.WriteStartArray();
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

    /// <summary>
    /// Stored entries per tag attribute value. Anything malformed reads as empty.
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
    /// One stored addressee: an object with the character beside the owner.
    /// </summary>
    private static Entry? ReadEntry(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object ||
            !item.TryGetProperty(OwnerField, out var owner) ||
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
    /// tooltip over the block names them as its recipients — so it splits on
    /// commas; a name that itself contains one resolves to nobody, which shows
    /// the block to nobody rather than to the wrong reader.
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
