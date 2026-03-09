using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Infrastructure.Persistence.Entities.Account;

namespace DM.Infrastructure.Persistence.Entities.Messaging;

/// <summary>
/// DAL model for global chat event participant.
/// For closed events, only participants can send messages in global chat during the event.
/// </summary>
[Table("GlobalChatEventParticipants")]
public class GlobalChatEventParticipant
{
    /// <summary>
    /// Participant link identifier
    /// </summary>
    [Key]
    public Guid GlobalChatEventParticipantId { get; set; }

    /// <summary>
    /// Event identifier
    /// </summary>
    public Guid GlobalChatEventId { get; set; }

    /// <summary>
    /// User identifier
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Whether this participant is an organizer.
    /// Organizers can manage the event: start, end, add/remove participants.
    /// </summary>
    public bool IsOrganizer { get; set; }

    /// <summary>
    /// When the participant joined the event
    /// </summary>
    public DateTimeOffset JoinedAtUtc { get; set; }

    /// <summary>
    /// The event
    /// </summary>
    [ForeignKey(nameof(GlobalChatEventId))]
    public virtual GlobalChatEvent GlobalChatEvent { get; set; } = null!;

    /// <summary>
    /// The user
    /// </summary>
    [ForeignKey(nameof(UserId))]
    public virtual User User { get; set; } = null!;
}
