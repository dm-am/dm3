using System;

namespace DM.Domain.Game.Features.Posts;

/// <summary>
/// DTO model for post creating
/// </summary>
public class CreatePost
{
    /// <summary>
    /// Room identifier
    /// </summary>
    public Guid RoomId { get; set; }

    /// <summary>
    /// Character identifier
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