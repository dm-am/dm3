using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Services.DataAccess.BusinessObjects.Administration;
using DM.Services.DataAccess.BusinessObjects.Common;
using DM.Services.DataAccess.BusinessObjects.DataContracts;
using DM.Services.DataAccess.BusinessObjects.Users;

namespace DM.Services.DataAccess.BusinessObjects.Messaging;

/// <summary>
/// DAL model for unified message (both global chat and private conversations)
/// </summary>
[Table("Messages")]
public class Message : IRemovable
{
    /// <summary>
    /// Well-known ID for global chat conversation
    /// </summary>
    public static readonly Guid GlobalChatId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    /// <summary>
    /// Message identifier
    /// </summary>
    [Key]
    public Guid MessageId { get; set; }

    /// <summary>
    /// Author identifier
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Conversation identifier (GlobalChatId for global chat)
    /// </summary>
    public Guid ConversationId { get; set; }

    /// <summary>
    /// Creation moment (UTC)
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Last modification moment (UTC)
    /// </summary>
    public DateTimeOffset? ModifiedUtc { get; set; }

    /// <summary>
    /// User who last updated the message
    /// </summary>
    public Guid? ModifiedByUserId { get; set; }

    /// <summary>
    /// Text content
    /// </summary>
    public string Text { get; set; }

    /// <inheritdoc />
    public bool IsRemoved { get; set; }

    /// <summary>
    /// User who deleted the message (for moderation)
    /// </summary>
    public Guid? DeletedByUserId { get; set; }

    /// <summary>
    /// When the message was deleted
    /// </summary>
    public DateTimeOffset? DeletedAtUtc { get; set; }

    /// <summary>
    /// Author
    /// </summary>
    [ForeignKey(nameof(UserId))]
    public virtual User Author { get; set; }

    /// <summary>
    /// User who last updated the message
    /// </summary>
    [ForeignKey(nameof(ModifiedByUserId))]
    public virtual User ModifiedBy { get; set; }

    /// <summary>
    /// User who deleted the message
    /// </summary>
    [ForeignKey(nameof(DeletedByUserId))]
    public virtual User DeletedBy { get; set; }

    /// <summary>
    /// Conversation
    /// </summary>
    [ForeignKey(nameof(ConversationId))]
    public virtual Conversation Conversation { get; set; }

    /// <summary>
    /// Edit history
    /// </summary>
    [InverseProperty(nameof(MessageEdit.Message))]
    public virtual ICollection<MessageEdit> Edits { get; set; }

    /// <summary>
    /// Likes
    /// </summary>
    [InverseProperty(nameof(Like.Message))]
    public virtual ICollection<Like> Likes { get; set; }

    /// <summary>
    /// Administrative warnings
    /// </summary>
    [InverseProperty(nameof(Warning.Message))]
    public virtual ICollection<Warning> Warnings { get; set; }
}
