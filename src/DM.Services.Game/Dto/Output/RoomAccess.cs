using System;
using DM.Services.Core.Dto;
using DM.Services.Core.Dto.Enums;

namespace DM.Services.Game.Dto.Output;

/// <summary>
/// DTO model for room access
/// </summary>
public class RoomAccess
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
    /// Type of participant (Character or Reader)
    /// </summary>
    public RoomAccessParticipantType ParticipantType { get; set; }

    /// <summary>
    /// Character (when ParticipantType = Character)
    /// </summary>
    public Character Character { get; set; } = null!;

    /// <summary>
    /// User who has access (reader or character author)
    /// </summary>
    public GeneralUser User { get; set; } = null!;

    /// <summary>
    /// When access was granted
    /// </summary>
    public DateTimeOffset GrantedUtc { get; set; }

    /// <summary>
    /// User who granted access
    /// </summary>
    public GeneralUser GrantedBy { get; set; } = null!;
}
