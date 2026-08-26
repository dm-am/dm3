namespace DM.Domain.Game.Features.Rooms;

/// <summary>
/// How long the text fields of a game room may be.
/// </summary>
/// <remarks>
/// One declaration because creation and editing must agree. They already
/// disagreed once, for games: the create validator read a shared constant while
/// the edit validator kept its own numbers, so a title creation accepted could
/// not be saved again after any change to the row.
/// </remarks>
internal static class RoomFieldLimits
{
    /// <summary>Longest room title.</summary>
    public const int TitleMaxLength = 100;
}
