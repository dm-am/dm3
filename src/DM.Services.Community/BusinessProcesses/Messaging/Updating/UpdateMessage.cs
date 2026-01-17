using System;

namespace DM.Services.Community.BusinessProcesses.Messaging.Updating;

/// <summary>
/// DTO for updating message
/// </summary>
public class UpdateMessage
{
    /// <summary>
    /// Message identifier
    /// </summary>
    public Guid MessageId { get; set; }

    /// <summary>
    /// Message content
    /// </summary>
    public string Text { get; set; }
}
