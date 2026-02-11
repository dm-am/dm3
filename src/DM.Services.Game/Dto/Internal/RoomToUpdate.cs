using DM.Services.Game.Dto.Output;

namespace DM.Services.Game.Dto.Internal;

/// <summary>
/// Internal DTO model for room updating
/// </summary>
internal class RoomToUpdate : Room
{
    /// <summary>
    /// Room parent game
    /// </summary>
    public Dto.Output.Game Game { get; set; } = null!;

    /// <summary>
    /// Previous room order info
    /// </summary>
    public RoomOrderInfo? PreviousRoom { get; set; }

    /// <summary>
    /// Next room order info
    /// </summary>
    public RoomOrderInfo? NextRoom { get; set; }
}