using System;

namespace DM.Domain.Messaging.Features.Messages;

/// <summary>
/// DTO for updating a message entity in repository: a field left null keeps the current value
/// </summary>
public class UpdateMessageEntity
{
    public Guid MessageId { get; set; }

    public string? Text { get; set; }

    /// <summary>
    /// Who edits: the message row keeps no editor, the edit history is the only record of it
    /// </summary>
    public Guid EditorUserId { get; set; }
}
