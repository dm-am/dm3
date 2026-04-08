using System;
using System.Collections.Generic;

namespace DM.Domain.Community.Features.Polls;

/// <summary>
/// DTO for creating a poll entity in repository
/// </summary>
public class CreatePollEntity
{
    /// <summary>
    /// Poll identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Poll start moment (UTC)
    /// </summary>
    public DateTime StartsUtc { get; set; }

    /// <summary>
    /// Poll end moment (UTC)
    /// </summary>
    public DateTime EndsUtc { get; set; }

    /// <summary>
    /// Poll title
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// Optional description/details for the poll
    /// </summary>
    public string? Details { get; set; }

    /// <summary>
    /// Whether poll is anonymous
    /// </summary>
    public bool IsAnonymous { get; set; } = true;

    /// <summary>
    /// Poll options
    /// </summary>
    public IEnumerable<CreatePollOptionEntity> Options { get; set; } = [];
}

/// <summary>
/// DTO for creating a poll option entity
/// </summary>
public class CreatePollOptionEntity
{
    /// <summary>
    /// Option identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Option text
    /// </summary>
    public string Text { get; set; } = null!;
}
