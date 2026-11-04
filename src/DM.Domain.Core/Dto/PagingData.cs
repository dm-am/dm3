namespace DM.Domain.Core.Dto;

/// <summary>
/// Paging data for repositories
/// </summary>
/// <remarks>
/// Converts skip/take pagination to internal paging result.
/// </remarks>
public class PagingData
{
    /// <summary>
    /// Create paging data from query
    /// </summary>
    /// <param name="query">Paging query with skip/take</param>
    /// <param name="defaultPageSize">Default page size if take is 0</param>
    /// <param name="totalCount">Total count of entities</param>
    public PagingData(PagingQuery query, int defaultPageSize, int totalCount)
    {
        var pageSize = query.Take > 0 ? query.Take : defaultPageSize;
        // 1-based entity number, widened so that Skip = int.MaxValue does not wrap.
        var entityNumber = (int)Math.Min((long)query.Skip + 1, int.MaxValue);

        Result = PagingResult.Create(totalCount, entityNumber, pageSize);
        Skip = query.Skip;
        Take = pageSize;
    }

    /// <summary>
    /// Number of entities to skip
    /// </summary>
    public int Skip { get; }

    /// <summary>
    /// Number of entities to take
    /// </summary>
    public int Take { get; }

    /// <summary>
    /// Paging result with totals and current page info
    /// </summary>
    public PagingResult Result { get; }
}
