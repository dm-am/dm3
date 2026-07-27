using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Infrastructure.Persistence.Entities.Account;

namespace DM.Infrastructure.Persistence.Entities.Messaging;

/// <summary>
/// DAL model for message edit history
/// </summary>
[Table("MessageEdits")]
public class MessageEdit
{
    /// <summary>
    /// Edit record identifier
    /// </summary>
    [Key]
    public Guid MessageEditId { get; set; }

    /// <summary>
    /// Message identifier
    /// </summary>
    public Guid MessageId { get; set; }

    /// <summary>
    /// Editor user identifier
    /// </summary>
    public Guid EditorUserId { get; set; }

    /// <summary>
    /// Edit timestamp (UTC)
    /// </summary>
    public DateTimeOffset ModifiedUtc { get; set; }

    /// <summary>
    /// Parent message
    /// </summary>
    [ForeignKey(nameof(MessageId))]
    public virtual Message Message { get; set; } = null!;

    /// <summary>
    /// Editor user
    /// </summary>
    [ForeignKey(nameof(EditorUserId))]
    public virtual User Editor { get; set; } = null!;
}
