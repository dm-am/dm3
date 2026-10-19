using System;

namespace DM.Domain.Moderation.Features.Tickets;

public class CreateTicket
{
    /// <summary>
    /// Target username who is being reported
    /// </summary>
    public string TargetUsername { get; set; } = "";

    /// <summary>
    /// Entity ID that caused the report (comment, message, etc.)
    /// </summary>
    public Guid? EntityId { get; set; }

    /// <summary>
    /// Type of entity (Comment, Message, Post, etc.)
    /// </summary>
    public string? EntityType { get; set; }

    /// <summary>
    /// Snapshot of the reported content
    /// </summary>
    public string Description { get; set; } = "";

    /// <summary>
    /// Reporter's explanation of the issue
    /// </summary>
    public string Comment { get; set; } = "";
}
