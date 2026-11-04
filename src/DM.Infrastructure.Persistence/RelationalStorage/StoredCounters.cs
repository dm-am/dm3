using System;
using System.Linq.Expressions;

namespace DM.Infrastructure.Persistence.RelationalStorage;

/// <summary>
/// A stored counter moved in the database, so two writers at once both count.
/// </summary>
internal static class StoredCounters
{
    /// <summary>The same counter, one higher.</summary>
    public static Expression<Func<TOwner, int>> Raised<TOwner>(Expression<Func<TOwner, int>> counter) =>
        Expression.Lambda<Func<TOwner, int>>(
            Expression.Add(counter.Body, Expression.Constant(1)), counter.Parameters);

    /// <summary>
    /// The same counter, one lower but never below zero: nothing recomputes the column,
    /// so a count that has drifted must not go negative and stay there.
    /// </summary>
    public static Expression<Func<TOwner, int>> Lowered<TOwner>(Expression<Func<TOwner, int>> counter) =>
        Expression.Lambda<Func<TOwner, int>>(
            Expression.Condition(
                Expression.GreaterThan(counter.Body, Expression.Constant(0)),
                Expression.Subtract(counter.Body, Expression.Constant(1)),
                Expression.Constant(0)),
            counter.Parameters);
}
