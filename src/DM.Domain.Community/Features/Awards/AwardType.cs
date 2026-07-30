using System;

namespace DM.Domain.Community.Features.Awards;

/// <summary>
/// Award type catalog — timeless. A specific contest series
/// is stored in <see cref="ContestSeries"/>, a grant in <see cref="UserAward"/>
/// with FKs to both. The icon comes from the game-icons sprite (validated on creation).
/// </summary>
public class AwardType
{
    /// <summary>Catalog record identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Stable code ("contest_first", "popular_vote", "guesser").</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Display name ("Литконкурс", "Народное признание").</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Description (what it is granted for).</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Icon name from the game-icons sprite.</summary>
    public string IconName { get; set; } = string.Empty;

    /// <summary>
    /// Tier for the visual style (1=gold, 2=silver, 3=bronze).
    /// For literary contest placements: 1/2/3 = 1st/2nd/3rd place. For special awards: 1.
    /// </summary>
    public int? Tier { get; set; }

    /// <summary>Display order within a contest series.</summary>
    public int SortOrder { get; set; }

    /// <summary>Active = available for granting. Soft-delete via the flag.</summary>
    public bool IsActive { get; set; }
}

/// <summary>Request to create a new record in the award catalog.</summary>
public class CreateAwardType
{
    /// <summary>Stable code.</summary>
    public string Code { get; set; } = string.Empty;
    /// <summary>Title.</summary>
    public string Title { get; set; } = string.Empty;
    /// <summary>Description.</summary>
    public string Description { get; set; } = string.Empty;
    /// <summary>Icon name.</summary>
    public string IconName { get; set; } = string.Empty;
    /// <summary>Tier (visual style).</summary>
    public int? Tier { get; set; }
    /// <summary>Display order within a series.</summary>
    public int SortOrder { get; set; }
}

/// <summary>
/// Partial update request. Any null field = leave untouched.
/// </summary>
public class UpdateAwardType
{
    /// <summary>Identifier of the record being updated.</summary>
    public Guid Id { get; set; }
    /// <summary>New title (null = leave unchanged).</summary>
    public string? Title { get; set; }
    /// <summary>New description (null = leave unchanged).</summary>
    public string? Description { get; set; }
    /// <summary>New icon (null = leave unchanged).</summary>
    public string? IconName { get; set; }
    /// <summary>New tier (null = leave unchanged).</summary>
    public int? Tier { get; set; }
    /// <summary>New SortOrder (null = leave unchanged).</summary>
    public int? SortOrder { get; set; }
    /// <summary>New IsActive (null = leave unchanged).</summary>
    public bool? IsActive { get; set; }
}
