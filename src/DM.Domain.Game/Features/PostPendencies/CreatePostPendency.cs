using System;

namespace DM.Domain.Game.Features.PostPendencies;

/// <summary>
/// DTO model for creating post pendencies
/// </summary>
/// <remarks>
/// No user in it. The one who waits is whoever asks, and the one waited for is
/// the player of the character: both are facts the server holds, and a body
/// that named them as well could only disagree with them.
/// </remarks>
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
}
