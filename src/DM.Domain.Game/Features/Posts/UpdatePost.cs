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
    /// Text
    /// </summary>
    public string Text { get; set; } = null!;

    /// <summary>
    /// Comment
    /// </summary>
    public string Comment { get; set; } = null!;

    /// <summary>
    /// Master message
    /// </summary>
    public string MasterMessage { get; set; } = null!;

    #region Internal fields (set by service)

    /// <summary>
    /// Modified timestamp (internal)
    /// </summary>
    public DateTimeOffset? ModifiedUtc { get; set; }

    /// <summary>
    /// Modified by user ID (internal)
    /// </summary>
    public Guid? ModifiedByUserId { get; set; }

    /// <summary>
    /// Soft delete flag (internal)
    /// </summary>
    public bool? IsRemoved { get; set; }

    #endregion
}