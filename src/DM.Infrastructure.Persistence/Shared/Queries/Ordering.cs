using System;
using System.Linq;
using System.Linq.Expressions;

namespace DM.Infrastructure.Persistence.Shared.Queries;

/// <summary>
/// One key ordered in the direction the reader asked for.
/// </summary>
internal static class Ordering
{
    public static IOrderedQueryable<T> OrderByDirection<T, TKey>(
        this IQueryable<T> source, Expression<Func<T, TKey>> key, bool ascending) =>
        ascending ? source.OrderBy(key) : source.OrderByDescending(key);

    public static IOrderedQueryable<T> ThenByDirection<T, TKey>(
        this IOrderedQueryable<T> source, Expression<Func<T, TKey>> key, bool ascending) =>
        ascending ? source.ThenBy(key) : source.ThenByDescending(key);
}
