using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DM.Infrastructure.Persistence.RelationalStorage;

internal static class DuplicateKey
{
    internal static bool IsDuplicateKey(this DbUpdateException exception) =>
        exception.InnerException is PostgresException inner && inner.IsDuplicateKey();

    internal static bool IsDuplicateKey(this PostgresException exception) =>
        exception.SqlState == PostgresErrorCodes.UniqueViolation;
}
