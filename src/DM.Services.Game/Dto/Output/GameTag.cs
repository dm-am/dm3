using System;

namespace DM.Services.Game.Dto.Output;

/// <summary>
/// DTO model for game tag
/// </summary>
public class GameTag
{
    /// <summary>
    /// Identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Tag group title
    /// </summary>
    public string GroupTitle { get; set; } = null!;

    /// <summary>
    /// Tag title
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// Number of active games with this tag
    /// </summary>
    public int GamesCount { get; set; }
}