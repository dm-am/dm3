using DM.Services.Core.Dto.Enums;

namespace DM.Web.API.Dto.Games;

/// <summary>
/// Request model for creating a new room
/// </summary>
public class CreateRoomRequest
{
    /// <summary>
    /// Room title
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// Room content type
    /// </summary>
    public RoomType Type { get; set; }

    /// <summary>
    /// Room access type
    /// </summary>
    public RoomAccessType AccessType { get; set; }

    /// <summary>
    /// Any user can read private messages within posts
    /// </summary>
    public bool ViewPrivateText { get; set; }

    /// <summary>
    /// Any user can see dice roll results
    /// </summary>
    public bool ViewDiceResults { get; set; } = true;

    /// <summary>
    /// Dice rolling is enabled in this room
    /// </summary>
    public bool DiceEnabled { get; set; } = true;
}
