using DM.Domain.Core.Dto;

namespace DM.Web.API.Shared.Dto;

/// <summary>
/// Paging DTO model
/// </summary>
public class Paging
{
    /// <inheritdoc />
    public Paging(PagingResult pagingResult)
    {
        Pages = pagingResult.TotalPagesCount;
        Current = pagingResult.CurrentPage;
        Size = pagingResult.PageSize;
        Number = pagingResult.EntityNumber;
        Total = pagingResult.TotalEntitiesCount;
    }

    /// <summary>
    /// Constructor for cursor-based pagination
    /// </summary>
    public Paging(bool hasMoreBefore, bool hasMoreAfter)
    {
        HasMoreBefore = hasMoreBefore;
        HasMoreAfter = hasMoreAfter;
    }

    /// <summary>
    /// Total pages count
    /// </summary>
    public int Pages { get; }

    /// <summary>
    /// Current page number
    /// </summary>
    public int Current { get; }

    /// <summary>
    /// Page size
    /// </summary>
    public int Size { get; }

    /// <summary>
    /// Entity number
    /// </summary>
    public int Number { get; }

    /// <summary>
    /// Total entity count
    /// </summary>
    public int Total { get; }

    /// <summary>
    /// Whether there are more messages before (older) the current set
    /// </summary>
    public bool? HasMoreBefore { get; }

    /// <summary>
    /// Whether there are more messages after (newer) the current set
    /// </summary>
    public bool? HasMoreAfter { get; }
}