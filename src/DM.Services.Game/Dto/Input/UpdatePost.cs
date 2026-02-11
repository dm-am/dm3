using System;
using DM.Services.Core.Dto;

namespace DM.Services.Game.Dto.Input;

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
    public string Commentary { get; set; } = null!;

    /// <summary>
    /// Master message
    /// </summary>
    public string MasterMessage { get; set; } = null!;
}