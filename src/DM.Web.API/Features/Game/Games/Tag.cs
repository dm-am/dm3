using System;

namespace DM.Web.API.Features.Game.Games;

/// <summary>
/// API DTO model for game tag
/// </summary>
public class Tag
{
    /// <summary>
    /// Identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Tag display name
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// Tag category name
    /// </summary>
    public string GroupTitle { get; set; } = null!;

    /// <summary>
    /// Number of active games with this tag
    /// </summary>
    public int GamesCount { get; set; }
}
