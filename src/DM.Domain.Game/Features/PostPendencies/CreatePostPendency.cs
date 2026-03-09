using System;

namespace DM.Domain.Game.Features.PostPendencies;

/// <summary>
/// DTO model for creating post pendencies
/// </summary>
public class CreatePostPendency
{
    /// <summary>
    /// Room identifier
    /// </summary>
    public Guid RoomId { get; set; }

    /// <summary>
    /// Character identifier (whose turn it is to post)
    /// </summary>
    public Guid CharacterId { get; set; }

    /// <summary>
    /// Username who is expected to post
    /// </summary>
    public string WaitingForUsername { get; set; } = null!;
}
