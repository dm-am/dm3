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
/// navigation to project through, so no projection can carry them and every read
/// used to answer with an empty list however many likes the row had. What that
/// cost is not a zero on a card: the like/unlike path asks the very same list
/// whether the viewer has already liked, so a repeat like was accepted and an
/// unlike was refused.
///
/// Two batched queries for the whole page instead of a correlated subquery per
/// row (PERFORMANCE.md -> "Avoid inline aggregations"): the (entity, liker) pairs
/// first, then one projection of the distinct likers.
///
/// The identifier list is a List&lt;Guid&gt;, NOT Guid[] - EF Core's translator
/// has a Guid[] edge case that throws TypeLoadException on the
/// ReadOnlySpan&lt;Guid&gt; interpreter path (see TopicRepository).
///
/// The query tags stay with the caller: they are what names the module a slow
/// query came from.
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
