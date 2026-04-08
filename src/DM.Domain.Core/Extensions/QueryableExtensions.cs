using System.Linq;
using DM.Domain.Core.Dto;

namespace DM.Domain.Core.Extensions;

/// <summary>
/// Paging query utils
/// </summary>
public static class QueryableExtensions
{
    /// <summary>
    /// Skip and take
    /// </summary>
    /// <param name="queryable">Ordered queryable to page</param>
    /// <param name="paging">Paging data</param>
    /// <typeparam name="T">Entity type</typeparam>
    /// <returns>Paged queryable</returns>
    public static IQueryable<T> Page<T>(this IOrderedQueryable<T> queryable,
        PagingData? paging)
    {
        return paging == null
            ? queryable
            : queryable.Skip(paging.Skip).Take(paging.Take);
    }
}
