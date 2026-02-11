using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using DM.Web.API.BbRendering;
using DM.Web.API.Dto.Users;

namespace DM.Web.API.Dto.Messaging;

/// <summary>
/// API DTO for message (both global chat and private conversations)
/// </summary>
public class Message
{
    /// <summary>
    /// Identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Creating moment (UTC)
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Last modification moment (UTC)
    /// </summary>
    public DateTimeOffset? ModifiedUtc { get; set; }

    /// <summary>
    /// Message author
    /// </summary>
    public User Author { get; set; } = null!;

    /// <summary>
    /// Message content
    /// </summary>
    public CommonBbText Text { get; set; } = null!;

    /// <summary>
    /// Message is deleted (soft delete)
    /// </summary>
    public bool IsRemoved { get; set; }

    /// <summary>
    /// User who deleted the message (for moderation)
    /// </summary>
    public User? DeletedBy { get; set; }

    /// <summary>
    /// When the message was deleted (UTC)
    /// </summary>
    public DateTimeOffset? DeletedAtUtc { get; set; }

    /// <summary>
    /// Edit history
    /// </summary>
    public IEnumerable<MessageEdit> Edits { get; set; } = [];

    /// <summary>
    /// Users who liked this message
    /// </summary>
    public IEnumerable<User> Likes { get; set; } = [];

    /// <summary>
    /// Global chat event identifier if message is part of an event
    /// </summary>
    public Guid? GlobalChatEventId { get; set; }
}

/// <summary>
/// Input DTO for creating a message
/// </summary>
public class CreateMessageInput
{
    /// <summary>
    /// Message text content (BBCode)
    /// </summary>
    [Required(ErrorMessage = "Message text is required")]
    [StringLength(10000, MinimumLength = 1, ErrorMessage = "Message text must be between 1 and 10000 characters")]
    public string Text { get; set; } = "";
}

/// <summary>
/// Input DTO for updating a message
/// </summary>
public class UpdateMessageInput
{
    /// <summary>
    /// Updated message text content (BBCode)
    /// </summary>
    [Required(ErrorMessage = "Message text is required")]
    [StringLength(10000, MinimumLength = 1, ErrorMessage = "Message text must be between 1 and 10000 characters")]
    public string Text { get; set; } = "";
}