using System;
using DM.Services.Core.Dto;

namespace DM.Services.Game.Dto.Output;

/// <summary>
/// Post pendency (who is expected to post in a room)
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
    /// Who created this expectation
    /// </summary>
    public GeneralUser CreatedBy { get; set; } = null!;

    /// <summary>
    /// Who is expected to write the post
    /// </summary>
    public GeneralUser WaitingForUser { get; set; } = null!;

    /// <summary>
    /// When the expectation was created
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// When the expectation was fulfilled
    /// </summary>
    public DateTimeOffset? FulfilledUtc { get; set; }
}
