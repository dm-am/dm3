using System;
using DM.Domain.Core.Dto;

namespace DM.Domain.Game.Features.Posts;

public class UpdatePost
{
    public Guid PostId { get; set; }

    public Optional<Guid>? CharacterId { get; set; }

    /// <summary>
    /// Null keeps the current value
    /// </summary>
    public string? GameText { get; set; }

    /// <summary>
    /// Null keeps the current value, an empty string clears it
    /// </summary>
    public string? MetagameText { get; set; }
}
