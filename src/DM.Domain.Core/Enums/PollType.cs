namespace DM.Domain.Core.Enums;

/// <summary>
/// Poll type
/// </summary>
public enum PollType
{
    /// <summary>
    /// Global site-wide poll
    /// </summary>
    Global = 0,

    /// <summary>
    /// Game-specific poll
    /// </summary>
    Game = 1,

    /// <summary>
    /// Forum topic poll
    /// </summary>
    Topic = 2
}
