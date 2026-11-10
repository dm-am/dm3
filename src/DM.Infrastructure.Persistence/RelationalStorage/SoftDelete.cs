using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Infrastructure.Persistence.Entities.Contracts;
using Microsoft.EntityFrameworkCore;

namespace DM.Infrastructure.Persistence.RelationalStorage;

/// <summary>
/// The one place a row records who removed it and when.
/// </summary>
/// <remarks>
/// One call keeps the flag and the two audit columns of <see cref="ISoftDeletable" /> together;
/// SoftDeleteAuditShould holds that they are written in this file alone.
/// </remarks>
internal static class SoftDelete
{
    /// <remarks>
    /// The author is nullable because some removals have none: a token consumed by the flow
    /// that issued it, a row swept by a background collector. Null there is the honest answer;
    /// null after a person pressed delete is the defect.
    /// </remarks>
    public static void Mark(ISoftDeletable entity, Guid? deletedByUserId, DateTimeOffset deletedUtc)
    {
        entity.IsRemoved = true;
        entity.DeletedByUserId = deletedByUserId;
        entity.DeletedUtc = deletedUtc;
    }

    public static Task<int> MarkWhere<TEntity>(
        IQueryable<TEntity> rows, Guid? deletedByUserId, DateTimeOffset deletedUtc, CancellationToken ct = default)
        where TEntity : class, ISoftDeletable =>
        rows.ExecuteUpdateAsync(s => s
            .SetProperty(x => x.IsRemoved, true)
            .SetProperty(x => x.DeletedByUserId, deletedByUserId)
            .SetProperty(x => x.DeletedUtc, (DateTimeOffset?)deletedUtc), ct);
}
