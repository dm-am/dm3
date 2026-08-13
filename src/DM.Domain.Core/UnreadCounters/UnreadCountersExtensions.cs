using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;

namespace DM.Domain.Core.UnreadCounters;

/// <summary>
/// Extension for helper methods to fill counters for entities
/// </summary>
public static class UnreadCountersExtensions
{
    /// <summary>
    /// Writes the markers of an entity that is about to be inserted, and hands back
    /// the reservation that takes them off again unless it is committed.
    /// </summary>
    /// <remarks>
    /// The only legal way to write a marker. Bound with <c>await using</c> and
    /// committed on the line after the insert returns — see
    /// <see cref="UnreadCountersReservation" /> for why the order is this way round.
    /// A marker refused midway takes back the ones already written before the
    /// refusal leaves this method, so a caller never sees a half-written set.
    /// </remarks>
    public static async Task<UnreadCountersReservation> ReserveAsync(
        this IUnreadCountersRepository repository, params UnreadMarker[] markers)
    {
        var reservation = new UnreadCountersReservation(repository);
        try
        {
            foreach (var marker in markers)
            {
                await reservation.WriteAsync(marker);
            }
        }
        catch
        {
            await reservation.DisposeAsync();
            throw;
        }

        return reservation;
    }

    /// <summary>
    /// Fill counters fields for passed parent entities (count of entities with unread)
    /// </summary>
    public static Task FillParentCounters<TEntity>(this IUnreadCountersRepository repository,
        ICollection<TEntity> entities, Guid userId,
        Func<TEntity, Guid> getId, Expression<Func<TEntity, int>> counterField) =>
        FillCounters(entities, userId, getId, repository.SelectByParentsAsync, counterField);

    /// <summary>
    /// Fill total unread counters fields for passed parent entities (sum of all unread)
    /// </summary>
    public static Task FillTotalUnreadCounters<TEntity>(this IUnreadCountersRepository repository,
        ICollection<TEntity> entities, Guid userId,
        Func<TEntity, Guid> getId, Expression<Func<TEntity, int>> counterField) =>
        FillCounters(entities, userId, getId, repository.SelectTotalUnreadByParentsAsync, counterField);

    /// <summary>
    /// Fill counters fields for passed entities
    /// </summary>
    public static Task FillEntityCounters<TEntity>(this IUnreadCountersRepository repository,
        ICollection<TEntity> entities, Guid userId,
        Func<TEntity, Guid> getId, Expression<Func<TEntity, int>> counterField,
        UnreadEntryType entryType = UnreadEntryType.Message) =>
        FillCounters(entities, userId, getId, repository.SelectByEntitiesAsync, counterField, entryType);

    private static async Task FillCounters<TEntity>(ICollection<TEntity> entities, Guid userId,
        Func<TEntity, Guid> getId, Func<Guid, UnreadEntryType, Guid[], Task<IDictionary<Guid, int>>> getCounters,
        Expression<Func<TEntity, int>> counterField, UnreadEntryType entryType = UnreadEntryType.Message)
    {
        if (counterField.Body is MemberExpression { Member: PropertyInfo property })
        {
            var counters = await getCounters(userId, entryType, entities.Select(getId).ToArray());
            foreach (var entity in entities)
            {
                property.SetValue(entity, counters[getId(entity)]);
            }
        }
    }
}
