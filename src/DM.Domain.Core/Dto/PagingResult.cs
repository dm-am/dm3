namespace DM.Domain.Core.Dto;

/// <summary>
/// DTO model for paged data
/// </summary>
public class PagingResult
{
    /// <summary>
    /// Total entities count of certain type across the filtered entities
    /// </summary>
    public int TotalEntitiesCount { get; private set; }

    public int CurrentPage { get; private set; }

    /// <summary>
    /// Page size
    /// </summary>
    public int PageSize { get; private set; }

    public static PagingResult Create(int totalEntitiesCount, int entityNumber, int pageSize)
    {
        return new PagingResult
        {
            TotalEntitiesCount = totalEntitiesCount,
            CurrentPage = Math.Max(1, (int)Math.Ceiling((decimal)entityNumber / pageSize)),
            PageSize = pageSize
        };
    }

    public static PagingResult Empty(int pageSize) => new()
    {
        TotalEntitiesCount = 0,
        CurrentPage = 1,
        PageSize = pageSize
    };
}
