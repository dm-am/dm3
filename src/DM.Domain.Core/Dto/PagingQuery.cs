using System.ComponentModel.DataAnnotations;

namespace DM.Domain.Core.Dto;

/// <summary>
/// Base query for pagination
/// </summary>
/// <remarks>
/// Standard pagination: ?skip=0&amp;take=20
/// Max take: 100
/// </remarks>
public class PagingQuery
{
    /// <summary>
    /// Number of records to skip
    /// </summary>
    [Range(0, int.MaxValue, ErrorMessage = "Skip must be non-negative")]
    public int Skip { get; set; } = 0;

    /// <summary>
    /// Number of records to take
    /// </summary>
    [Range(1, 100, ErrorMessage = "Take must be between 1 and 100")]
    public int Take { get; set; } = 20;

    /// <summary>
    /// Empty query to get paging information without fetching data
    /// </summary>
    public static PagingQuery Empty => new() { Take = 0 };
}
