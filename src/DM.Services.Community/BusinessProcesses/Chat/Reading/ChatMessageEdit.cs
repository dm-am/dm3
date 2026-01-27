using System;
using DM.Services.Core.Dto;

namespace DM.Services.Community.BusinessProcesses.Chat.Reading;

/// <summary>
/// DTO model for chat message edit record
/// </summary>
public class ChatMessageEdit
{
    /// <summary>
    /// Edit record identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Edit timestamp
    /// </summary>
    public DateTimeOffset EditedAtUtc { get; set; }

    /// <summary>
    /// Editor user
    /// </summary>
    public GeneralUser Editor { get; set; }
}
