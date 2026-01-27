using System;
using System.Collections.Generic;
using DM.Web.API.BbRendering;
using DM.Web.API.Dto.Users;

namespace DM.Web.API.Dto.Messaging;

/// <summary>
/// DTO model for chat message
/// </summary>
public class ChatMessage
{
    /// <summary>
    /// Identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Creating moment
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Last modification moment
    /// </summary>
    public DateTimeOffset? ModifiedUtc { get; set; }

    /// <summary>
    /// Author
    /// </summary>
    public User Author { get; set; }

    /// <summary>
    /// User who deleted the message (for moderation)
    /// </summary>
    public User DeletedBy { get; set; }

    /// <summary>
    /// When the message was deleted
    /// </summary>
    public DateTimeOffset? DeletedAtUtc { get; set; }

    /// <summary>
    /// Edit history
    /// </summary>
    public IEnumerable<ChatMessageEdit> Edits { get; set; }

    /// <summary>
    /// Content
    /// </summary>
    public ChatBbText Text { get; set; }

    /// <summary>
    /// Message is deleted (soft delete)
    /// </summary>
    public bool IsRemoved { get; set; }

    /// <summary>
    /// Users who liked this message
    /// </summary>
    public IEnumerable<User> Likes { get; set; }
}