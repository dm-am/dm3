namespace DM.Domain.Core.Enums;

/// <summary>
/// Game room access mode
/// </summary>
public enum RoomAccessType
{
    /// <summary>
    /// Anyone can view the room
    /// </summary>
    Open = 0,

    /// <summary>
    /// Only linked character players may view the room
    /// </summary>
    Private = 1
}
