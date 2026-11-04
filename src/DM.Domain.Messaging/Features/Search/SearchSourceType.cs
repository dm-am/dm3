namespace DM.Domain.Messaging.Features.Search;

/// <summary>
/// Source kind a search scope refers to.
/// </summary>
public enum SearchSourceType
{
    /// <summary>Global chat.</summary>
    Global,

    /// <summary>A specific direct/group chat.</summary>
    Chat,

    /// <summary>A specific game (its readable posts).</summary>
    Game
}
