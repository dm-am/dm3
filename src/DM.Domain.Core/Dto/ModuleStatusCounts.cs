namespace DM.Domain.Core.Dto;

/// <summary>
/// Count breakdown by module status (for games/blogs)
/// </summary>
public class ModuleStatusCounts
{
    /// <summary>
    /// Number of items in Draft status
    /// </summary>
    public int Draft { get; set; }

    /// <summary>
    /// Number of items in Active status
    /// </summary>
    public int Active { get; set; }

    /// <summary>
    /// Number of items in Closed status
    /// </summary>
    public int Closed { get; set; }

    /// <summary>
    /// Total count (sum of all statuses)
    /// </summary>
    public int Total => Draft + Active + Closed;
}
