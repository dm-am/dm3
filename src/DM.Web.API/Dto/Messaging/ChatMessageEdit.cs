using System;
using DM.Web.API.Dto.Users;

namespace DM.Web.API.Dto.Messaging;

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
    public User Editor { get; set; }
}
