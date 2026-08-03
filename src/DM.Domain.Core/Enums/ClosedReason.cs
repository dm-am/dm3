namespace DM.Domain.Core.Enums;

/// <summary>
/// Reason why a game was closed (only applicable when Status = Closed)
/// </summary>
public enum ClosedReason
{
    /// <summary>
    /// Game was simply closed, no one plans to return
    /// </summary>
    None = 0,

    /// <summary>
    /// Game was completed successfully (story finished)
    /// </summary>
    Finished = 1,

    /// <summary>
    /// Game was temporarily frozen, participants plan to return
    /// </summary>
    Frozen = 2
}
