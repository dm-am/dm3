using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace DM.Infrastructure.Persistence.RelationalStorage;

/// <summary>
/// The one source of the serial a readable page address is encoded from.
/// </summary>
/// <remarks>
/// Taking the number from the sequence before the insert writes the row with its final address
/// in one statement. Not a transaction around two writes: a strategy replay re-attaches the
/// entity with SerialNumber already filled, and the identity column inserts it verbatim.
/// A rolled back insert leaves a gap, as any identity column does. Public because the seeder
/// writes the same three tables.
/// </remarks>
public static class SerialNumberAllocator
{
    /// <summary>
    /// The next value of the SerialNumber identity sequence of
    /// <typeparamref name="TEntity" />'s table.
    /// </summary>
    public static async Task<int> NextAsync<TEntity>(
        DmDbContext dbContext,
        CancellationToken ct = default)
        where TEntity : class
    {
        // The table name comes from the model, quoted: pg_get_serial_sequence parses it as SQL does.
        var table = $"\"{dbContext.Model.FindEntityType(typeof(TEntity))!.GetTableName()}\"";

        var serialNumber = await dbContext.Database
            .SqlQuery<long>(
                $"SELECT nextval(pg_get_serial_sequence({table}, 'SerialNumber')) AS \"Value\"")
            .FirstAsync(ct);

        return (int)serialNumber;
    }
}
