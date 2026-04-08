using System.ComponentModel;

namespace DM.Domain.Core.Enums;

/// <summary>
/// Filter for game recruitment status (only applicable when Status = Active)
/// </summary>
public enum RecruitmentFilter
{
    /// <summary>
    /// Show all active games regardless of recruitment status
    /// </summary>
    [Description("Любой")]
    Any = 0,

    /// <summary>
    /// Show only games with open recruitment (any type)
    /// </summary>
    [Description("Открыт")]
    Open = 1,

    /// <summary>
    /// Show only games with closed recruitment
    /// </summary>
    [Description("Закрыт")]
    Closed = 2,

    /// <summary>
    /// Show only games with first-time recruitment (RecruitmentCount = 1)
    /// </summary>
    [Description("Первый набор")]
    Initial = 3,

    /// <summary>
    /// Show only games with subsequent recruitment (RecruitmentCount >= 2, донабор)
    /// </summary>
    [Description("Донабор игроков")]
    Subsequent = 4
}
