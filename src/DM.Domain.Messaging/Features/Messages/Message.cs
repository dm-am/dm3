using System;
using System.Collections.Generic;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Likes;

namespace DM.Domain.Messaging.Features.Messages;

/// <summary>
/// Service DTO of message
/// </summary>
public class Message : ILikable
{
    /// <summary>
    /// Message identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <inheritdoc />
    public LikeEntityType LikeEntityType => LikeEntityType.Message;

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
    public GeneralUser Author { get; set; } = null!;

    /// <summary>
    /// Message content
    /// </summary>
    public string Text { get; set; } = null!;

    /// <summary>
    /// Message is deleted (soft delete)
    /// </summary>
    public bool IsRemoved { get; set; }

    /// <summary>
    /// User who deleted the message (for moderation)
    /// </summary>
    public GeneralUser DeletedBy { get; set; } = null!;

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
    public IEnumerable<GeneralUser> Likes { get; set; } = [];

    /// <summary>
    /// Global chat event identifier if message is part of an event
    /// </summary>
    public Guid? GlobalChatEventId { get; set; }

    /// <summary>
    /// Chat identifier
    /// </summary>
    public Guid ChatId { get; set; }

    /// <summary>
    /// Chat type (Direct, Group, Global)
    /// </summary>
    public ChatType ChatType { get; set; }
}
