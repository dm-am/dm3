using System;
using DM.Domain.Core.Enums;

namespace DM.Domain.Community.Features.Awards;

/// <summary>
/// Contest series. Each contest is a separate record with a global
/// sequential <see cref="Number"/> within its type. Awards (UserAward)
/// reference the series via FK; award types (<see cref="AwardType"/>)
/// are timeless and are not duplicated for each year.
/// </summary>
public class ContestSeries
{
    /// <summary>Identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Contest type (literary, art, etc.).</summary>
    public ContestType ContestType { get; set; }

    /// <summary>
    /// Sequential contest number within the type (23rd literary, 1st art).
    /// UNIQUE(ContestType, Number).
    /// </summary>
    public int Number { get; set; }

    /// <summary>Year held (shown in the year badge on the tile).</summary>
    public int Year { get; set; }

    /// <summary>Link to the forum topic with contest results (optional).</summary>
    public string? TopicUrl { get; set; }

    /// <summary>Active = available for granting awards (in the dropdown).</summary>
    public bool IsActive { get; set; }
}

/// <summary>Request to create a new series.</summary>
public class CreateContestSeries
{
    /// <summary>Contest type.</summary>
    public ContestType ContestType { get; set; }
    /// <summary>Sequential number within the type.</summary>
    public int Number { get; set; }
    /// <summary>Year.</summary>
    public int Year { get; set; }
    /// <summary>Link to the results topic (optional).</summary>
    public string? TopicUrl { get; set; }
}

/// <summary>Request to partially update a series.</summary>
public class UpdateContestSeries
{
    /// <summary>Identifier of the record being updated.</summary>
    public Guid Id { get; set; }
    /// <summary>New ContestType (null = leave unchanged).</summary>
    public ContestType? ContestType { get; set; }
    /// <summary>New Number (null = leave unchanged).</summary>
    public int? Number { get; set; }
    /// <summary>New Year (null = leave unchanged).</summary>
    public int? Year { get; set; }
    /// <summary>New TopicUrl (null = leave unchanged; empty string = clear).</summary>
    public string? TopicUrl { get; set; }
    /// <summary>New IsActive (null = leave unchanged).</summary>
    public bool? IsActive { get; set; }
}
