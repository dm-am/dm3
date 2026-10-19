using System;

namespace DM.Domain.Community.Features.Awards;

/// <summary>
/// Award type catalog — timeless. A specific contest series
/// is stored in <see cref="ContestSeries"/>, a grant in <see cref="UserAward"/>
/// with FKs to both. The icon comes from the game-icons sprite (validated on creation).
/// </summary>
public class AwardType
{
    public Guid Id { get; set; }

    /// <summary>Stable code ("contest_first", "popular_vote", "guesser").</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Display name ("Литконкурс", "Народное признание, например" - the
    /// trailing words are part of that award's name, not a stray comment).</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Description (what it is granted for).</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Icon name from the game-icons sprite.</summary>
    public string IconName { get; set; } = string.Empty;

    /// <summary>
    /// The metal of the badge on <see cref="Catalogs.TierScale"/>, null for none. On a
    /// literary contest gold, silver and bronze are the 1st/2nd/3rd places; the
    /// honorary record in the catalog is platinum.
    /// </summary>
    public int? Tier { get; set; }

    /// <summary>Display order within a contest series.</summary>
    public int SortOrder { get; set; }

    /// <summary>Active = available for granting. Soft-delete via the flag.</summary>
    public bool IsActive { get; set; }
}

public class CreateAwardType
{
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string IconName { get; set; } = string.Empty;
    /// <summary>Tier on <see cref="Catalogs.TierScale"/>.</summary>
    public int? Tier { get; set; }
    /// <summary>Display order within a series.</summary>
    public int SortOrder { get; set; }
}

/// <summary>
/// Partial update request. Any null field = leave untouched.
/// </summary>
public class UpdateAwardType
{
    public Guid Id { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? IconName { get; set; }
    public int? Tier { get; set; }
    public int? SortOrder { get; set; }
    public bool? IsActive { get; set; }
}
