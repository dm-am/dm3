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
}
