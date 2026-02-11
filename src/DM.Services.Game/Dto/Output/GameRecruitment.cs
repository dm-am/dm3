using System;

namespace DM.Services.Game.Dto.Output;

/// <summary>
/// DTO for game recruitment information
/// </summary>
public class GameRecruitment
{
    /// <summary>
    /// Recruitment is open for new players
    /// </summary>
    public bool IsOpen { get; set; }

    /// <summary>
    /// Maximum number of players allowed (null = unlimited)
    /// </summary>
    public int? PlayerLimit { get; set; }

    /// <summary>
    /// Current number of active players
    /// </summary>
    public int PlayerCount { get; set; }

    /// <summary>
    /// When the recruitment was started
    /// </summary>
    public DateTimeOffset? StartedUtc { get; set; }
}
