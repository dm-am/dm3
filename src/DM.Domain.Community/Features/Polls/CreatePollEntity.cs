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
    /// Poll start date
    /// </summary>
    public DateTime StartDate { get; set; }

    /// <summary>
    /// Poll end date
    /// </summary>
    public DateTime EndDate { get; set; }

    /// <summary>
    /// Whether the poll is global
    /// </summary>
    public bool Global { get; set; }

    /// <summary>
    /// Poll title
    /// </summary>
    public string Title { get; set; } = null!;

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
