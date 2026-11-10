using DM.Domain.Core.Dto;

namespace DM.Domain.Core.Extensions;

/// <summary>
/// Paging query utils
/// </summary>
public static class QueryableExtensions
{
    public static IQueryable<T> Page<T>(this IOrderedQueryable<T> queryable,
        PagingData? paging)
    {
        return paging == null
            ? queryable
            : queryable.Skip(paging.Skip).Take(paging.Take);
    }
}
