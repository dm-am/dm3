using System;
using DM.Web.API.Features.Community.Users;

namespace DM.Web.API.Features.Messaging.Messages;

/// <summary>
/// API DTO for message edit record
/// </summary>
public class MessageEdit
{
    /// <summary>
    /// Edit record identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Edit timestamp (UTC)
    /// </summary>
    public DateTimeOffset EditedUtc { get; set; }

    /// <summary>
    /// Editor user
    /// </summary>
    public User? Editor { get; set; }
}
