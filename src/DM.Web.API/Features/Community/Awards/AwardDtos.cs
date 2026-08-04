using System;
using DM.Domain.Core.Enums;

namespace DM.Web.API.Features.Community.Awards;

/// <summary>API DTO: award type catalog record (timeless).</summary>
public class AwardType
{
    /// <summary>Identifier.</summary>
    public Guid Id { get; set; }
    /// <summary>Stable code ("contest_first", "popular_vote").</summary>
    public string Code { get; set; } = string.Empty;
    /// <summary>Display name ("Литконкурс", "Народное признание, например" - the
    /// trailing words are part of that award's name).</summary>
    public string Title { get; set; } = string.Empty;
    /// <summary>Description (what it is granted for).</summary>
    public string Description { get; set; } = string.Empty;
    /// <summary>Icon name from the game-icons sprite.</summary>
    public string IconName { get; set; } = string.Empty;
    /// <summary>Visual tier: 1 gold, 2 silver, 3 bronze, 4 steel, 5 diamond. Null for no tier.</summary>
    public int? Tier { get; set; }
    /// <summary>Display order within a contest series.</summary>
    public int SortOrder { get; set; }
    /// <summary>Whether the type is active.</summary>
    public bool IsActive { get; set; }
}

/// <summary>API DTO: contest series.</summary>
public class ContestSeries
{
    /// <summary>Identifier.</summary>
    public Guid Id { get; set; }
    /// <summary>Contest type.</summary>
    public ContestType ContestType { get; set; }
    /// <summary>Sequential contest number within the type (23rd literary, 1st art).</summary>
    public int Number { get; set; }
    /// <summary>Year held (shown in the year badge on the tile).</summary>
    public int Year { get; set; }
    /// <summary>Link to the forum topic with results (optional).</summary>
    public string? TopicUrl { get; set; }
    /// <summary>Whether the series is active (visible in the dropdown).</summary>
    public bool IsActive { get; set; }
}

/// <summary>API DTO: an award granted to a user.</summary>
public class UserAward
{
    /// <summary>Grant record identifier.</summary>
    public Guid Id { get; set; }
    /// <summary>Award type.</summary>
    public AwardType Type { get; set; } = null!;
    /// <summary>Contest series (optional, for future out-of-contest awards).</summary>
    public ContestSeries? ContestSeries { get; set; }
    /// <summary>
    /// Link to the forum topic with the work the award was granted for
    /// (optional — best_critic / guesser are not tied to a work).
    /// </summary>
    public string? WorkUrl { get; set; }
    /// <summary>Grant moment (UTC).</summary>
    public DateTimeOffset AwardedUtc { get; set; }
}

/// <summary>Request to create an award type catalog record.</summary>
public class CreateAwardTypeRequest
{
    /// <summary>Stable code. Unique.</summary>
    public string Code { get; set; } = string.Empty;
    /// <summary>Title.</summary>
    public string Title { get; set; } = string.Empty;
    /// <summary>Description (what it is granted for).</summary>
    public string Description { get; set; } = string.Empty;
    /// <summary>Icon name.</summary>
    public string IconName { get; set; } = string.Empty;
    /// <summary>Visual tier.</summary>
    public int? Tier { get; set; }
    /// <summary>Display order.</summary>
    public int SortOrder { get; set; }
}

/// <summary>Request to partially update an award type. Null fields = leave untouched.</summary>
public class UpdateAwardTypeRequest
{
    /// <summary>New title.</summary>
    public string? Title { get; set; }
    /// <summary>New description.</summary>
    public string? Description { get; set; }
    /// <summary>New icon.</summary>
    public string? IconName { get; set; }
    /// <summary>New tier.</summary>
    public int? Tier { get; set; }
    /// <summary>New order.</summary>
    public int? SortOrder { get; set; }
    /// <summary>New IsActive.</summary>
    public bool? IsActive { get; set; }
}

/// <summary>Request to create a contest series.</summary>
public class CreateContestSeriesRequest
{
    /// <summary>Contest type.</summary>
    public ContestType ContestType { get; set; }
    /// <summary>Sequential number within the type.</summary>
    public int Number { get; set; }
    /// <summary>Year.</summary>
    public int Year { get; set; }
    /// <summary>Topic link (optional).</summary>
    public string? TopicUrl { get; set; }
}

/// <summary>Request to partially update a series. Null fields = leave untouched.</summary>
public class UpdateContestSeriesRequest
{
    /// <summary>New ContestType.</summary>
    public ContestType? ContestType { get; set; }
    /// <summary>New Number.</summary>
    public int? Number { get; set; }
    /// <summary>New Year.</summary>
    public int? Year { get; set; }
    /// <summary>New TopicUrl (null = leave unchanged, "" = clear).</summary>
    public string? TopicUrl { get; set; }
    /// <summary>New IsActive.</summary>
    public bool? IsActive { get; set; }
}

/// <summary>Request to grant an award to a user.</summary>
public class GrantUserAwardRequest
{
    /// <summary>Award type identifier from the catalog.</summary>
    public Guid AwardTypeId { get; set; }
    /// <summary>Contest series identifier (optional).</summary>
    public Guid? ContestSeriesId { get; set; }
    /// <summary>Link to the forum topic with the work (optional).</summary>
    public string? WorkUrl { get; set; }
}
