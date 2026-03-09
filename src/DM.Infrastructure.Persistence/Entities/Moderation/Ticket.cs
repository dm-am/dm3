using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Infrastructure.Persistence.Entities.Account;
using DM.Domain.Core.Enums;

namespace DM.Infrastructure.Persistence.Entities.Moderation;

/// <summary>
/// DAL model of user complaint ticket
/// </summary>
[Table("Tickets")]
public class Ticket
{
    /// <summary>
    /// Ticket identifier
    /// </summary>
    [Key]
    public Guid TicketId { get; set; }

    /// <summary>
    /// Complaint author identifier
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Complaint target user identifier
    /// </summary>
    public Guid TargetId { get; set; }

    /// <summary>
    /// Entity that caused the complaint (comment, message, post, etc.)
    /// </summary>
    public Guid? EntityId { get; set; }

    /// <summary>
    /// Type of entity that caused the complaint
    /// </summary>
    public string EntityType { get; set; } = null!;

    /// <summary>
    /// Ticket status
    /// </summary>
    public TicketStatus Status { get; set; }

    /// <summary>
    /// Creation moment
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Last update moment
    /// </summary>
    public DateTimeOffset? UpdatedUtc { get; set; }

    /// <summary>
    /// Causation entity text (snapshot of the reported content)
    /// </summary>
    public string Description { get; set; } = null!;

    /// <summary>
    /// Reporter comment explaining the issue
    /// </summary>
    public string Comment { get; set; } = null!;

    /// <summary>
    /// Moderator who is handling this ticket
    /// </summary>
    public Guid? AssignedModeratorId { get; set; }

    /// <summary>
    /// Complaint responsible identifier
    /// </summary>
    public Guid? AnswerAuthorId { get; set; }

    /// <summary>
    /// Moment when ticket was resolved
    /// </summary>
    public DateTimeOffset? ResolvedUtc { get; set; }

    /// <summary>
    /// Moderator's response/resolution comment
    /// </summary>
    public string? Answer { get; set; }

    /// <summary>
    /// Warning issued as a result of this ticket (if any)
    /// </summary>
    public Guid? WarningId { get; set; }

    /// <summary>
    /// Ban issued as a result of this ticket (if any)
    /// </summary>
    public Guid? BanId { get; set; }

    #region Navigation Properties

    /// <summary>
    /// Complaint author
    /// </summary>
    [ForeignKey(nameof(UserId))]
    public virtual User Author { get; set; } = null!;

    /// <summary>
    /// Complaint target user
    /// </summary>
    [ForeignKey(nameof(TargetId))]
    public virtual User Target { get; set; } = null!;

    /// <summary>
    /// Moderator assigned to handle this ticket
    /// </summary>
    [ForeignKey(nameof(AssignedModeratorId))]
    public virtual User? AssignedModerator { get; set; }

    /// <summary>
    /// Moderator who resolved the ticket
    /// </summary>
    [ForeignKey(nameof(AnswerAuthorId))]
    public virtual User? AnswerAuthor { get; set; }

    /// <summary>
    /// Warning issued as result of this ticket
    /// </summary>
    [ForeignKey(nameof(WarningId))]
    public virtual Warning? Warning { get; set; }

    /// <summary>
    /// Ban issued as result of this ticket
    /// </summary>
    [ForeignKey(nameof(BanId))]
    public virtual Ban? Ban { get; set; }

    /// <summary>
    /// Responses in this ticket conversation
    /// </summary>
    public virtual ICollection<TicketResponse> Responses { get; set; } = [];

    #endregion
}
