using System;

namespace DM.Domain.Game.Features.Games;

public class GameTag
{
    public Guid Id { get; set; }

    /// <summary>
    /// Numeric alias for URL filtering
    /// </summary>
    public int ShortId { get; set; }

    public string GroupTitle { get; set; } = null!;

    public string? GroupDescription { get; set; }

    public int GroupSortOrder { get; set; }

    /// <summary>
    /// How many tags of this tag's group one game may carry; null means no limit
    /// </summary>
    public int? GroupMaxTagsPerGame { get; set; }

    public string Title { get; set; } = null!;

    /// <summary>
    /// Tag description as typed: the hint surface, whose one tag is
    /// [tip text="..."]word[/tip] or [tip img="address"]word[/tip]
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Within the group
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// Active games with this tag
    /// </summary>
    public int GamesCount { get; set; }
}
