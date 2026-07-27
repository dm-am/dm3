using System;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query;
using MongoDB.Driver;

namespace DM.Infrastructure.Persistence.Shared.Queries;

/// <summary>
/// Upper-bound handling for user-supplied inclusive date range filters.
/// A date-only "to" value (midnight) means "the whole selected day",
/// so the bound extends to the next midnight compared exclusively;
/// values with a time component keep the inclusive comparison as-is.
/// At the calendar edge, where the next midnight does not exist,
/// the inclusive comparison is kept to avoid overflow.
/// </summary>
public static class DateRangeFilters
{
    /// <summary>
    /// Value carries no time component and should cover the whole selected day
    /// </summary>
    public static bool IsWholeDay(DateTimeOffset value) => value.TimeOfDay == TimeSpan.Zero;

    /// <summary>
    /// Filter by an inclusive "to" bound over a date property
    /// </summary>
    public static IQueryable<T> WhereAtOrBefore<T>(
        this IQueryable<T> source,
        Expression<Func<T, DateTimeOffset>> property,
        DateTimeOffset to)
    {
        if (IsWholeDay(to) && CanExtendToNextDay(to))
        {
            var bound = to.AddDays(1);
            return source.Where(Compose(property, d => d < bound));
        }
        return source.Where(Compose(property, d => d <= to));
    }

    /// <summary>
    /// Filter by an inclusive "to" bound over an optional date property.
    /// Entities without the date are excluded.
    /// </summary>
    public static IQueryable<T> WhereAtOrBefore<T>(
        this IQueryable<T> source,
        Expression<Func<T, DateTimeOffset?>> property,
        DateTimeOffset to)
    {
        if (IsWholeDay(to) && CanExtendToNextDay(to))
        {
            var bound = to.AddDays(1);
            return source.Where(Compose(property, d => d.HasValue && d.Value < bound));
        }
        return source.Where(Compose(property, d => d.HasValue && d.Value <= to));
    }

    /// <summary>
    /// Mongo variant of the inclusive "to" bound for UTC date fields
    /// </summary>
    public static FilterDefinition<T> AtOrBefore<T>(
        Expression<Func<T, DateTime>> field,
        DateTimeOffset to)
    {
        return IsWholeDay(to) && CanExtendToNextDay(to)
            ? Builders<T>.Filter.Lt(field, to.AddDays(1).UtcDateTime)
            : Builders<T>.Filter.Lte(field, to.UtcDateTime);
    }

    /// <summary>
    /// AddDays(1) operates on the clock part of the value, so the check
    /// must use it too: an offset instant can be below DateTimeOffset.MaxValue
    /// while its clock date is already the last representable day
    /// </summary>
    private static bool CanExtendToNextDay(DateTimeOffset to) => to.DateTime <= DateTime.MaxValue.AddDays(-1);

    /// <summary>
    /// Substitute the predicate parameter with the property body to keep
    /// call sites free of hand-written expression trees
    /// </summary>
    private static Expression<Func<T, bool>> Compose<T, TValue>(
        Expression<Func<T, TValue>> property,
        Expression<Func<TValue, bool>> predicate)
    {
        var body = ReplacingExpressionVisitor.Replace(predicate.Parameters[0], property.Body, predicate.Body);
        return Expression.Lambda<Func<T, bool>>(body, property.Parameters);
    }
}
