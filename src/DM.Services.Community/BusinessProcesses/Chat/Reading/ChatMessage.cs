using System;
using System.Collections.Generic;
using DM.Services.Common.Dto;
using DM.Services.Core.Dto;

namespace DM.Services.Community.BusinessProcesses.Chat.Reading;

/// <summary>
/// DTO model for chat message
/// </summary>
public class ChatMessage : ILikable
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
    /// Last modification moment (computed from edit history, UTC)
    /// </summary>
    public DateTimeOffset? ModifiedUtc { get; set; }

    /// <summary>
    /// Message author
    /// </summary>
    public GeneralUser Author { get; set; }

    /// <summary>
    /// Content
    /// </summary>
    public string Text { get; set; }

    /// <summary>
    /// Message is deleted (soft delete)
    /// </summary>
    public bool IsRemoved { get; set; }

    /// <summary>
    /// User who deleted the message (for moderation)
    /// </summary>
    public GeneralUser DeletedBy { get; set; }

    /// <summary>
    /// When the message was deleted
    /// </summary>
    public DateTimeOffset? DeletedAtUtc { get; set; }

    /// <summary>
    /// Edit history
    /// </summary>
    public IEnumerable<ChatMessageEdit> Edits { get; set; }

    /// <summary>
    /// Users who liked this message
    /// </summary>
    public IEnumerable<GeneralUser> Likes { get; set; }
}