using System;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.Characters;

namespace DM.Domain.Game.Features.RoomAccesses;

public class RoomAccess
{
    public Guid Id { get; set; }

    public Guid RoomId { get; set; }

    public RoomAccessTargetType TargetType { get; set; }

    /// <summary>
    /// What the grant admits: the row itself opens the room for reading,
    /// Full is what admits writing in it
    /// </summary>
    public RoomAccessPolicy Policy { get; set; }

    /// <summary>
    /// When TargetType is Character, null for a reader grant
    /// </summary>
    public Character? Character { get; set; }

    /// <summary>
    /// The reader, or the character author
    /// </summary>
    public GeneralUser User { get; set; } = null!;
}
