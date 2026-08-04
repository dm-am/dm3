using System;
using DM.Domain.Core.Dto;

namespace DM.Domain.Game.Features.Posts;

/// <summary>
/// DTO model for post updating
/// </summary>
public class UpdatePost
{
    /// <summary>
    /// Post identifier
    /// </summary>
    public Guid PostId { get; set; }

    /// <summary>
    /// Character identifier
    /// </summary>
    public Optional<Guid>? CharacterId { get; set; }

    /// <summary>
    /// Game text (in-character content). Null keeps the current value.
    /// </summary>
    public string? GameText { get; set; }

    /// <summary>
    /// Metagame text (OOC commentary). Null keeps the current value,
    /// an empty string clears it.
    /// </summary>
    public string? MetagameText { get; set; }

    #region Internal fields (set by service)

    /// <summary>
    /// Soft delete flag (internal)
    /// </summary>
    public bool? IsRemoved { get; set; }

    #endregion
}
