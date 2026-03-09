using DM.Domain.Core.Enums;

namespace DM.Domain.Core.Events;

/// <summary>
/// Domain event data transfer object
/// </summary>
public class DomainEvent
{
    /// <summary>
    /// Event type (e.g., "new game created" or "post added to game")
    /// </summary>
    public EventType Type { get; set; }

    /// <summary>
    /// Entity identifier (e.g., the new game ID or the affected post ID)
    /// </summary>
    public Guid EntityId { get; set; }
}
