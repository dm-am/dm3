using System;
using System.Text.Json.Serialization;
using DM.Domain.Core.Dto;

namespace DM.Web.API.Shared.Dto;

/// <summary>
/// Offset-based pagination info in API response
/// </summary>
/// <remarks>
/// Contains information about pagination state for paginated responses.
/// Uses skip/take pattern per API standard.
/// </remarks>
public class PagingInfo
{
    /// <summary>
    /// Creates pagination info from service result
    /// </summary>
    public PagingInfo(PagingResult pagingResult)
    {
        Skip = (pagingResult.CurrentPage - 1) * pagingResult.PageSize;
        Take = pagingResult.PageSize;
        Total = pagingResult.TotalEntitiesCount;
    }

    /// <summary>
    /// Creates pagination info with explicit values
    /// </summary>
    public PagingInfo(int skip, int take, int total)
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
