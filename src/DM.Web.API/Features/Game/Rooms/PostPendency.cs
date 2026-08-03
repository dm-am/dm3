using System;
using DM.Web.API.Features.Community.Users;

namespace DM.Web.API.Features.Game.Rooms;

/// <summary>
/// DTO model for post pendency (expectation that someone will post)
/// </summary>
public class PostPendency
{
    /// <summary>
    /// Identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Room identifier
    /// </summary>
    public Guid RoomId { get; set; }

    /// <summary>
    /// Character identifier (whose turn it is to post)
    /// </summary>
    public Guid CharacterId { get; set; }

    /// <summary>
    /// Character name (whose turn it is to post). Always set on read; nullable
    /// because the same shape is the create body, and a non-nullable string
    /// there would fail implicit-required model validation.
    /// </summary>
    public string? CharacterName { get; set; }

    /// <summary>
    /// User who created this expectation
    /// </summary>
    public User CreatedBy { get; set; } = null!;

    /// <summary>
    /// User who is expected to write the post
    /// </summary>
    public User? WaitingFor { get; set; }

    /// <summary>
    /// When the expectation was created
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// When the expectation was fulfilled
    /// </summary>
    public DateTimeOffset? FulfilledUtc { get; set; }
}
