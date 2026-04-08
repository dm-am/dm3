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
    /// Game text (in-character content)
    /// </summary>
    public string GameText { get; set; } = null!;

    /// <summary>
    /// Metagame text (OOC commentary)
    /// </summary>
    public string MetagameText { get; set; } = null!;

    #region Internal fields (set by service)

    /// <summary>
    /// Soft delete flag (internal)
    /// </summary>
    public bool? IsRemoved { get; set; }

    #endregion
}