using System;
using DM.Domain.Core.Dto;

namespace DM.Web.API.Shared.Dto;

/// <summary>
/// Offset-based pagination info in API response
/// </summary>
public class PagingInfo
{
    // Both constructors are internal, so the assembly scan leaves the response shape alone.
    internal PagingInfo(PagingResult pagingResult)
    {
        Skip = (pagingResult.CurrentPage - 1) * pagingResult.PageSize;
        Take = pagingResult.PageSize;
        Total = pagingResult.TotalEntitiesCount;
    }

    internal PagingInfo(int skip, int take, int total)
    {
        Skip = skip;
        Take = take;
        Total = total;
    }

    /// <summary>
    /// Number of items skipped
    /// </summary>
    public int Skip { get; }

    /// <summary>
    /// Number of items per page
    /// </summary>
    public int Take { get; }

    /// <summary>
    /// Total item count
    /// </summary>
    public int Total { get; }

    /// <summary>
    /// Current page number (1-based)
    /// </summary>
    public int Current => Take > 0 ? (Skip / Take) + 1 : 1;

    /// <summary>
    /// Total number of pages
    /// </summary>
    public int Pages => Take > 0 ? (int)Math.Ceiling((double)Total / Take) : 1;
}
