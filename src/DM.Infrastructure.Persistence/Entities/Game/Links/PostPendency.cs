using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Infrastructure.Persistence.Entities.Game.Characters;
using DM.Infrastructure.Persistence.Entities.Game.Posts;
using DM.Infrastructure.Persistence.Entities.Account;

namespace DM.Infrastructure.Persistence.Entities.Game.Links;

/// <summary>
/// DAL model for post pendency (when someone is expected to post in a room)
/// </summary>
[Table("PostPendencies")]
public class PostPendency
{
    /// <summary>
    /// Pendency identifier
    /// </summary>
    [Key]
    public Guid PendencyId { get; set; }

    /// <summary>
    /// Room identifier
    /// </summary>
    public Guid RoomId { get; set; }

    /// <summary>
    /// Character identifier (whose turn it is to post)
    /// </summary>
    public Guid CharacterId { get; set; }

    /// <summary>
    /// User identifier who is expected to write a post (optional, for NPC or general expectations)
    /// </summary>
    public Guid? WaitingForUserId { get; set; }

    /// <summary>
    /// User identifier who created the expectation
    /// </summary>
    public Guid CreatedById { get; set; }

    /// <summary>
    /// Creation moment
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Moment when the expectation was fulfilled
    /// </summary>
    public DateTimeOffset? FulfilledUtc { get; set; }

    /// <summary>
    /// Moment when the last reminder was sent (null = never reminded)
    /// </summary>
    public DateTimeOffset? LastReminderUtc { get; set; }

    /// <summary>
    /// Room
    /// </summary>
    [ForeignKey(nameof(RoomId))]
    public virtual Room Room { get; set; } = null!;

    /// <summary>
    /// Character
    /// </summary>
    [ForeignKey(nameof(CharacterId))]
    public virtual Character Character { get; set; } = null!;

    /// <summary>
    /// User who is expected to write a post
    /// </summary>
    [ForeignKey(nameof(WaitingForUserId))]
    public virtual User? WaitingForUser { get; set; }

    /// <summary>
    /// User who created the expectation
    /// </summary>
    [ForeignKey(nameof(CreatedById))]
    public virtual User CreatedBy { get; set; } = null!;
}
