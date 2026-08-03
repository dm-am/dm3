namespace DM.Domain.Core.Enums;

/// <summary>
/// Participation scope for the games list player filter
/// (only applicable when PlayerUsername is set)
/// </summary>
public enum PlayerParticipation
{
    /// <summary>
    /// Player currently has an active character in the game (default)
    /// </summary>
    Active = 0,

    /// <summary>
    /// Player has any non-NPC character except declined applications:
    /// active, retired (dead / left / exiled) or an application under review
    /// </summary>
    Any = 1
}
