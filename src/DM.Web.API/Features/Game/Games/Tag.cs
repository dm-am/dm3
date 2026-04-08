namespace DM.Web.API.Features.Game.Games;

/// <summary>
/// API DTO model for game tag
/// </summary>
public class Tag
{
    /// <summary>
    /// Short numeric identifier for URL filtering (1, 2, 3...)
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Tag display name
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// Tag description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Sort order within group
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// Tag category name
    /// </summary>
    public string GroupTitle { get; set; } = null!;

    /// <summary>
    /// Tag category description
    /// </summary>
    public string? GroupDescription { get; set; }

    /// <summary>
    /// Tag category sort order
    /// </summary>
    public int GroupSortOrder { get; set; }

    /// <summary>
    /// Number of active games with this tag
    /// </summary>
    public int GamesCount { get; set; }
}
