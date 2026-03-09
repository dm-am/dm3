using System.ComponentModel;

namespace DM.Domain.Core.Enums;

/// <summary>
/// User list sorting options
/// </summary>
public enum UserSort
{
    /// <summary>
    /// Sort by username alphabetically (default)
    /// </summary>
    [Description("По имени")]
    Name = 0,

    /// <summary>
    /// Sort by rating (post review score sum) descending
    /// </summary>
    [Description("По рейтингу")]
    Rating = 1
}
