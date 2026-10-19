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

    /// <summary>The earliest year a series may carry.</summary>
    /// <remarks>
    /// The site opened in 2007 and its first contest was held in 2011. The bound is
    /// the opening rather than the first contest, so a series carried over from DM2
    /// is never refused by a year nobody has checked against its records.
    /// </remarks>
    public const int MinYear = 2007;

    /// <summary>How many years past the current one a series may carry.</summary>
    /// <remarks>
    /// A series is created when the contest is announced, which may be weeks before
    /// the new year. Anything further is a mistyped year, and the series page does
    /// not edit the year, so a typo would stay.
    /// </remarks>
    public const int YearsAhead = 1;

    /// <summary>The latest year a series may carry at the given moment.</summary>
    public static int LatestYear(DateTimeOffset now) => now.Year + YearsAhead;

    /// <summary>Link to the forum topic with contest results (optional).</summary>
    public string? TopicUrl { get; set; }

    /// <summary>Active = offered in the list of series when an award is granted.</summary>
    public bool IsActive { get; set; }
}

public class CreateContestSeries
{
    public ContestType ContestType { get; set; }
    /// <summary>Sequential number within the type.</summary>
    public int Number { get; set; }
    public int Year { get; set; }
    /// <summary>Link to the results topic (optional).</summary>
    public string? TopicUrl { get; set; }
}

/// <summary>Request to partially update a series; a null field is left as it is.</summary>
public class UpdateContestSeries
{
    public Guid Id { get; set; }
    public ContestType? ContestType { get; set; }
    public int? Number { get; set; }
    public int? Year { get; set; }
    /// <summary>An empty string clears the link.</summary>
    public string? TopicUrl { get; set; }
    public bool? IsActive { get; set; }
}
