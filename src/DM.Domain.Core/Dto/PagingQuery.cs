using System.ComponentModel.DataAnnotations;

namespace DM.Domain.Core.Dto;

/// <summary>
/// Base query for pagination
/// </summary>
/// <remarks>
/// Standard pagination: ?skip=0&amp;take=20; the largest take any endpoint serves is
/// <see cref="PagingPolicy.MaxPageSize"/>.
/// </remarks>
public class PagingQuery
{
    /// <summary>
    /// Number of records to skip
    /// </summary>
    [Range(0, int.MaxValue, ErrorMessage = "Смещение не может быть отрицательным")]
    public int Skip { get; set; }

    /// <summary>
    /// Number of records to take
    /// </summary>
    [Range(1, PagingPolicy.MaxPageSize, ErrorMessage = "Размер страницы от 1 до 200")]
    public int Take { get; set; } = PagingPolicy.DefaultPageSize;

    /// <summary>
    /// Empty query to get paging information without fetching data
    /// </summary>
    public static PagingQuery Empty => new() { Take = 0 };
}
