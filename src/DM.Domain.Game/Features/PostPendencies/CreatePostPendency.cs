using System;

namespace DM.Domain.Game.Features.PostPendencies;

/// <remarks>
/// No user in it: the one who waits is whoever asks, the one waited for is the player of the character.
/// </remarks>
public class CreatePostPendency
{
    public Guid RoomId { get; set; }

    /// <summary>
    /// Whose turn it is to post
    /// </summary>
    public Guid CharacterId { get; set; }
}
