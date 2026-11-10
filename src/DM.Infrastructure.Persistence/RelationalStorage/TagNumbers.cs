using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace DM.Infrastructure.Persistence.RelationalStorage;

/// <summary>
/// The one source of the number a tag is addressed by.
/// </summary>
/// <remarks>
/// A sequence (DmDbContext.TagShortIdSequence), not MAX + 1: two concurrent creates get
/// different numbers, and the number of a deleted tag is never handed out again, so a saved
/// /games link keeps pointing at its tag.
/// </remarks>
internal static class TagNumbers
{
    public static async Task<int> NextAsync(DmDbContext dbContext, CancellationToken ct = default)
    {
        // Raw: an interpolation hole would become a parameter, and nextval takes a regclass.
        // The name is a quoted constant; nextval returns bigint whatever the sequence type.
        var shortId = await dbContext.Database
            .SqlQueryRaw<long>($"SELECT nextval('\"{DmDbContext.TagShortIdSequence}\"') AS \"Value\"")
            .FirstAsync(ct);

        return (int)shortId;
    }
}
