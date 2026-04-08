using System.ComponentModel;

namespace DM.Domain.Core.Enums;

/// <summary>
/// Reason why a game was closed (only applicable when Status = Closed)
/// </summary>
public enum ClosedReason
{
    /// <summary>
    /// Game was simply closed, no one plans to return
    /// </summary>
    [Description("Закрыта")]
    None = 0,

    /// <summary>
    /// Game was completed successfully (story finished)
    /// </summary>
    [Description("Завершена")]
    Finished = 1,

    /// <summary>
    /// Game was temporarily frozen, participants plan to return
    /// </summary>
    [Description("Заморожена")]
    Frozen = 2
}
