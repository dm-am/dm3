using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DM.Infrastructure.Persistence.RelationalStorage;

/// <summary>
/// The unique-constraint refusal of Postgres, spelled once.
/// </summary>
internal static class DuplicateKey
{
    /// <summary>Whether the save was refused by a unique constraint</summary>
    internal static bool IsDuplicateKey(this DbUpdateException exception) =>
        exception.InnerException is PostgresException inner && inner.IsDuplicateKey();

    /// <summary>Whether the statement was refused by a unique constraint</summary>
    internal static bool IsDuplicateKey(this PostgresException exception) =>
        exception.SqlState == PostgresErrorCodes.UniqueViolation;
}
