using System;

namespace DM.Domain.Messaging.Features.Messages;

/// <summary>
/// DTO for updating a message entity in repository
/// </summary>
public class UpdateMessageEntity
{
    /// <summary>
    /// Message identifier
    /// </summary>
    public Guid MessageId { get; set; }

    /// <summary>
    /// Updated text (null to keep current)
    /// </summary>
    public string? Text { get; set; }

    /// <summary>
    /// Updated removed status (null to keep current)
    /// </summary>
    public bool? IsRemoved { get; set; }

    /// <summary>
    /// User performing the edit. The message row keeps an author and no editor,
    /// so the edit history is the only record that it was changed at all.
    /// </summary>
    public Guid EditorUserId { get; set; }
}
