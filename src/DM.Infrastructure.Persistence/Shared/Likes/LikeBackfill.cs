using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence.Shared.Users;
using Microsoft.EntityFrameworkCore;

namespace DM.Infrastructure.Persistence.Shared.Likes;

/// <summary>
/// Fills in who liked each row of a page.
/// </summary>
/// <remarks>
/// Likes live in the polymorphic Likes table (EntityType + EntityId) with no
/// navigation to project through. Two batched queries for the whole page: the
/// (entity, liker) pairs, then one projection of the distinct likers.
///
/// The identifier list is a List&lt;Guid&gt;, not Guid[]: EF Core's translator throws
/// TypeLoadException on a Guid[] (the ReadOnlySpan&lt;Guid&gt; interpreter path).
///
/// The query tags stay with the caller: they name the module a slow query came from.
/// </remarks>
internal static class LikeBackfill
{
    public static async Task Fill<TEntity>(
        DmDbContext dbContext,
        IReadOnlyCollection<TEntity> entities,
        LikeEntityType entityType,
        Func<TEntity, Guid> idOf,
        Action<TEntity, GeneralUser[]> assign,
        string pairsTag,
        string likersTag,
        CancellationToken ct)
    {
        if (entities.Count == 0)
        {
            return;
        }

        var entityIds = entities.Select(idOf).ToList();
        var pairs = await dbContext.Likes
            .TagWith(pairsTag)
            .AsNoTracking()
            // EF.Constant, not the bare argument: the two copies this replaced
            // named the type inline, so the discriminator reached Postgres as a
            // literal the planner has statistics for.
            .Where(l =>
                !l.IsRemoved &&
                l.EntityType == EF.Constant(entityType) &&
                entityIds.Contains(l.EntityId))
            .Select(l => new { l.EntityId, l.UserId })
            .ToListAsync(ct);

        if (pairs.Count == 0)
        {
            return;
        }

        var likerIds = pairs.Select(p => p.UserId).Distinct().ToList();
        var likers = await dbContext.Users
            .TagWith(likersTag)
            .AsNoTracking()
            .Where(u => likerIds.Contains(u.UserId))
            .ProjectToGeneralUser()
            .ToDictionaryAsync(u => u.UserId, ct);

        // A liker filtered out by the soft-delete filter has no projection; their
        // like is dropped rather than crashing the page.
        var byEntity = pairs
            .Where(p => likers.ContainsKey(p.UserId))
            .GroupBy(p => p.EntityId)
            .ToDictionary(g => g.Key, g => g.Select(p => likers[p.UserId]).ToArray());

        foreach (var entity in entities)
        {
            if (byEntity.TryGetValue(idOf(entity), out var likes))
            {
                assign(entity, likes);
            }
        }
    }
}
