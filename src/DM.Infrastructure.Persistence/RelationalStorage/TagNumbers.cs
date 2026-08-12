using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace DM.Infrastructure.Persistence.RelationalStorage;

/// <summary>
/// SSOT for the number a tag is addressed by.
/// </summary>
/// <remarks>
/// ShortId is the tag's public key: a /games link is written in it, and the required,
/// optional and excluded predicates compare by it. It was handed out as MAX + 1 over the
/// table, which fails in two directions. Two creates in one moment read one maximum and
/// both commit it, and the required-tag filter counts matching rows rather than distinct
/// tags — so a game carrying both halves of a shared number answers a two-tag search while
/// holding one of the two. And a tag is deleted physically, so the maximum also walks
/// backwards: the number of the tag just deleted is handed to the next one, and every saved
/// link written in it now points at a different tag.
///
/// A sequence answers both. It is taken outside the transaction that will use the value, so
/// two callers cannot be given one number, and it never returns to a number it has issued.
/// The unique index on ShortId refuses a duplicate that arrives some other way.
///
/// Declared on the model (DmDbContext.TagShortIdSequence) rather than written into the
/// migration by hand, for the reason the bootstrap seed is: regenerating the migration is
/// the documented way to change the schema, and a database object the model does not know
/// about does not survive it. Nothing binds the sequence to the property — the value is
/// read here and written as an ordinary column — which is the shape SerialNumberAllocator
/// has for the identity sequences behind readable page addresses.
/// </remarks>
internal static class TagNumbers
{
    /// <summary>
    /// The next number no tag has carried.
    /// </summary>
    public static async Task<int> NextAsync(DmDbContext dbContext, CancellationToken ct = default)
    {
        // Raw rather than interpolated into a query: an interpolation hole becomes a
        // parameter, and nextval takes a regclass rather than text. The name is a
        // compile-time constant, so there is nothing here for a caller to inject into. It
        // is quoted because the identifier is case-sensitive and nextval parses its
        // argument the way SQL parses a name, and the result is read as long because
        // nextval returns bigint whatever type the sequence was declared with.
        var shortId = await dbContext.Database
            .SqlQueryRaw<long>($"SELECT nextval('\"{DmDbContext.TagShortIdSequence}\"') AS \"Value\"")
            .FirstAsync(ct);

        return (int)shortId;
    }
}
