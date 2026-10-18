using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace DM.Testing;

/// <summary>
/// Values a default query object does not carry, for walking every field of it.
/// </summary>
/// <remarks>
/// The cache-key rules walk a query by reflection rather than through a list,
/// because a list has the defect the rules look for: a field is added, the
/// hand-kept key and the hand-kept "may be cached" check both forget it, and two
/// listings share one entry. One generator for all of them, so a type that is
/// learned here is learned by every walk at once.
/// </remarks>
public static class QueryProbes
{
    /// <summary>The properties a caller can set on a query of the given type.</summary>
    public static IReadOnlyList<PropertyInfo> WritableProperties(Type queryType) => queryType
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(property => property.CanWrite)
        .ToList();

    /// <summary>Values of the given type that a default query does not carry.</summary>
    public static IReadOnlyCollection<object> Candidates(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;

        if (underlying == typeof(string))
        {
            // "asc" is here because one of these strings is read as an order and
            // the rest of the alphabet means the same thing as no value at all.
            return ["distinct", "asc"];
        }

        if (underlying == typeof(int))
        {
            // Both Skip and Take are plain integers with defaults of their own, so
            // a constant would silently match one of them and check nothing.
            return [17];
        }

        if (underlying == typeof(bool))
        {
            return [true, false];
        }

        if (underlying == typeof(DateTimeOffset))
        {
            return [new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)];
        }

        if (underlying == typeof(Guid))
        {
            // Without this a collection of identifiers produced no candidate at
            // all and walked past the rule unchecked.
            return [Guid.Parse("00000000-0000-0000-0000-00000000c0de")];
        }

        if (underlying.IsEnum)
        {
            return Enum.GetValues(underlying).Cast<object>().ToArray();
        }

        return Collections(underlying);
    }

    /// <summary>One-element collections of whatever the property holds.</summary>
    private static IReadOnlyCollection<object> Collections(Type type)
    {
        if (!type.IsGenericType || !typeof(IEnumerable).IsAssignableFrom(type))
        {
            return [];
        }

        var item = type.GetGenericArguments()[0];

        return Candidates(item)
            .Select(value =>
            {
                var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(item))!;
                list.Add(value);

                return (object)list;
            })
            .ToArray();
    }
}
