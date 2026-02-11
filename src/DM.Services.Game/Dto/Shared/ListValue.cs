namespace DM.Services.Game.Dto.Shared;

/// <summary>
/// DTO model for a possible attribute list value
/// </summary>
public class ListValue
{
    /// <summary>
    /// Value
    /// </summary>
    public string Value { get; set; } = null!;

    /// <summary>
    /// Modifier
    /// </summary>
    public int? Modifier { get; set; }
}