using System.ComponentModel;

namespace DM.Domain.Core.Enums;

/// <summary>
/// Module status (unified for games and blogs)
/// </summary>
public enum ModuleStatus
{
    /// <summary>
    /// Module is being created/drafted
    /// </summary>
    [Description("Оформляется")]
    Draft = 0,

    /// <summary>
    /// Module is active (running, recruiting, or playing)
    /// </summary>
    [Description("Активен")]
    Active = 1,

    /// <summary>
    /// Module is closed (finished, frozen, or abandoned)
    /// </summary>
    [Description("Закрыт")]
    Closed = 2
}

/// <summary>
/// Premoderation status for newbie GMs/bloggers
/// </summary>
public enum PremoderationStatus
{
    /// <summary>
    /// Module does not require premoderation
    /// </summary>
    [Description("Одобрено")]
    Approved = 0,

    /// <summary>
    /// Module is awaiting mentor approval
    /// </summary>
    [Description("Ожидает проверки")]
    AwaitingApproval = 1,

    /// <summary>
    /// Module requires edits after mentor review
    /// </summary>
    [Description("Требует правок")]
    AwaitingEdits = 2
}
