namespace DM.Domain.Game.Features.Rooms;

// One declaration, because creation and editing must agree.
internal static class RoomFieldLimits
{
    /// <summary>
    /// Longest room title. The column is text and holds any length: the bound keeps a pasted page
    /// out of the game menu, where every title of the game stands.
    /// </summary>
    public const int TitleMaxLength = 100;
}
