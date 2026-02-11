using System;

namespace DM.Services.Game.Dto.Input;

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
    /// User login who is expected to post
    /// </summary>
    public string WaitingForUserLogin { get; set; } = null!;
}
