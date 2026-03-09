using System;
using System.Collections.Generic;
using DM.Domain.Core.Enums;

namespace DM.Domain.Personal.Features.Notifications;

/// <summary>
/// DTO model for notification creating
/// </summary>
public record CreateNotification
{
    /// <summary>
    /// Interested user identifiers
    /// </summary>
    public IEnumerable<Guid> UsersInterested { get; set; } = [];

    /// <summary>
    /// Event type
    /// </summary>
    public EventType EventType { get; set; }

    /// <summary>
    /// Event metadata
    /// </summary>
    public object Metadata { get; set; } = null!;
}