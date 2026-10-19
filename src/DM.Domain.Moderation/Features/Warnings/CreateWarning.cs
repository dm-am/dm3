using System;

namespace DM.Domain.Moderation.Features.Warnings;

public class CreateWarning
{
    public string Username { get; set; } = "";

    /// <summary>
    /// Entity ID that caused the warning. Not read for a warning on a profile,
    /// whose entity is the warned user.
    /// </summary>
    public Guid? EntityId { get; set; }

    /// <summary>
    /// Entity type: Comment, Message, Topic, or Profile for a warning on the
    /// profile of the warned user
    /// </summary>
    public string? EntityType { get; set; }

    /// <summary>
    /// Warning points, 0 being a verbal warning
    /// </summary>
    public int Points { get; set; } = 1;

    public string Reason { get; set; } = "";
}
