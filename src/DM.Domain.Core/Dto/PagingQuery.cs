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
    [Range(0, int.MaxValue, ErrorMessage = "Смещение не может быть отрицательным")]
    public int Skip { get; set; } = 0;

    /// <summary>
    /// Number of records to take
    /// </summary>
    // The ceiling is the largest size a reader may save as a preference: a
    // preference the API then refuses to serve is a setting that does nothing.
    [Range(1, 200, ErrorMessage = "Размер страницы от 1 до 200")]
    public int Take { get; set; } = PagingPolicy.DefaultPageSize;

    /// <summary>
    /// Empty query to get paging information without fetching data
    /// </summary>
    public static PagingQuery Empty => new() { Take = 0 };
}
