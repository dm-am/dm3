using System;

namespace DM.Web.API.Features.Game.Posts;

/// <summary>
/// Request model for creating a new post
/// </summary>
public class CreatePostRequest
{
    /// <summary>
    /// Character identifier (optional for master posts)
    /// </summary>
    public Guid? CharacterId { get; set; }

    /// <summary>
    /// Game text (in-character content)
    /// </summary>
    public string GameText { get; set; } = null!;

    /// <summary>
    /// Metagame text (OOC commentary)
    /// </summary>
    public string? MetagameText { get; set; }
}
