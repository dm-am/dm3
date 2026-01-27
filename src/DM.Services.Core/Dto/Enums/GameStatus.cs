using System.ComponentModel;

namespace DM.Services.Core.Dto.Enums;

/// <summary>
/// Game status (simplified to 3 states)
/// </summary>
public enum GameStatus
{
    /// <summary>
    /// Game is being created/drafted
    /// </summary>
    [Description("Оформляется")]
    Draft = 0,

    /// <summary>
    /// Game is running (recruiting or playing)
    /// </summary>
    [Description("Идет игра")]
    Active = 1,

    /// <summary>
    /// Game is closed (finished, frozen, or abandoned)
    /// Check IsFinished and IsFrozen flags for details
    /// </summary>
    [Description("Закрыта")]
    Closed = 2
}

/// <summary>
/// Premoderation status for new GM games
/// </summary>
public enum PremoderationStatus
{
    /// <summary>
    /// Game does not require premoderation
    /// </summary>
    [Description("Одобрено")]
    Approved = 0,

    /// <summary>
    /// Game is awaiting mentor approval
    /// </summary>
    [Description("Ожидает проверки")]
    AwaitingApproval = 1,

    /// <summary>
    /// Game requires edits after mentor review
    /// </summary>
    [Description("Требует правок")]
    AwaitingEdits = 2
}