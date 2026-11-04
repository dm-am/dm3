using System;
using DM.Domain.Core.Dto;

namespace DM.Domain.Game.Features.PostPendencies;

/// <summary>
/// Who is expected to post in a room
/// </summary>
public class PostPendency
{
    public Guid Id { get; set; }

    public Guid RoomId { get; set; }

    public Guid CharacterId { get; set; }

    /// <summary>
    /// Sent along: some screens that name the character have no character list to resolve the id
    /// </summary>
    public string CharacterName { get; set; } = null!;

    public GeneralUser CreatedBy { get; set; } = null!;

    public GeneralUser WaitingForUser { get; set; } = null!;

    public DateTimeOffset CreatedUtc { get; set; }

    public DateTimeOffset? FulfilledUtc { get; set; }
}
