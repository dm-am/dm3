using System;
using System.Collections.Generic;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Likes;

namespace DM.Domain.Messaging.Features.Messages;

public class Message : ILikable
{
    public Guid Id { get; set; }

    /// <inheritdoc />
    public LikeEntityType LikeEntityType => LikeEntityType.Message;

    public DateTimeOffset CreatedUtc { get; set; }

    public DateTimeOffset? ModifiedUtc { get; set; }

    /// <summary>
    /// Message author, null for a removed account
    /// </summary>
    public GeneralUser Author { get; set; } = null!;

    public string Text { get; set; } = null!;

    /// <summary>
    /// Message is deleted (soft delete)
    /// </summary>
    public bool IsRemoved { get; set; }

    /// <summary>
    /// Who deleted the message, null while it stands
    /// </summary>
    public GeneralUser DeletedBy { get; set; } = null!;

    public DateTimeOffset? DeletedUtc { get; set; }

    public IEnumerable<MessageEdit> Edits { get; set; } = [];

    public IEnumerable<GeneralUser> Likes { get; set; } = [];

    /// <summary>
    /// Global chat event the message was written during, if any
    /// </summary>
    public Guid? GlobalChatEventId { get; set; }

    public Guid ChatId { get; set; }

    public ChatType ChatType { get; set; }
}
