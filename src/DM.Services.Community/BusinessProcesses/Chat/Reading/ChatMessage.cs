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
    /// Creating moment
    /// </summary>
    public DateTimeOffset CreateDate { get; set; }

    /// <summary>
    /// Last modification moment
    /// </summary>
    public DateTimeOffset? LastUpdateDate { get; set; }

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
    /// Users who liked this message
    /// </summary>
    public IEnumerable<GeneralUser> Likes { get; set; }
}