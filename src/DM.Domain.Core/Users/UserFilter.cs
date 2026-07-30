using System;
using DM.Domain.Core.Enums;

namespace DM.Domain.Core.Users;

/// <summary>
/// Filter and sort for the public user list, passed as one object rather than a
/// run of positional arguments: the bounds below are interchangeable
/// <see cref="int" />? min/max pairs, so a misplaced argument would still compile.
/// </summary>
public record UserFilter
{
    /// <summary>
    /// Activity bucket: Active, Inactive, All, or the moderator-only Pending
    /// </summary>
    public UserActivityFilter Activity { get; init; }

    /// <summary>
    /// Optional fuzzy search by current or former username
    /// </summary>
    public string? Search { get; init; }

    /// <summary>
    /// Optional role filter
    /// </summary>
    public UserRole? Role { get; init; }

    /// <summary>
    /// Sort field. Ignored by the count, which needs the same filter but no order.
    /// </summary>
    public UserSort Sort { get; init; } = UserSort.Name;

    /// <summary>
    /// Sort direction. Ascending unless the caller decided otherwise — the default
    /// direction per sort field is a presentation rule and stays in the API layer.
    /// </summary>
    public bool SortAscending { get; init; } = true;

    /// <summary>
    /// Newbie filter: true = only newbies, false = only experienced, null = all
    /// </summary>
    public bool? IsNewbie { get; init; }

    /// <summary>
    /// Online filter: only users active within the online window
    /// </summary>
    public bool? IsOnline { get; init; }

    /// <summary>
    /// Minimum rating (post review score sum)
    /// </summary>
    public int? MinRating { get; init; }

    /// <summary>
    /// Maximum rating (post review score sum)
    /// </summary>
    public int? MaxRating { get; init; }

    /// <summary>
    /// Minimum number of games hosted (master or assistant)
    /// </summary>
    public int? MinGamesHosting { get; init; }

    /// <summary>
    /// Maximum number of games hosted (master or assistant)
    /// </summary>
    public int? MaxGamesHosting { get; init; }

    /// <summary>
    /// Minimum number of games played (has a character)
    /// </summary>
    public int? MinGamesPlaying { get; init; }

    /// <summary>
    /// Maximum number of games played (has a character)
    /// </summary>
    public int? MaxGamesPlaying { get; init; }

    /// <summary>
    /// Minimum number of blogs hosted (owner or assistant)
    /// </summary>
    public int? MinBlogsHosting { get; init; }

    /// <summary>
    /// Maximum number of blogs hosted (owner or assistant)
    /// </summary>
    public int? MaxBlogsHosting { get; init; }

    /// <summary>
    /// Registration date range start (inclusive)
    /// </summary>
    public DateTimeOffset? RegisteredFromUtc { get; init; }

    /// <summary>
    /// Registration date range end (inclusive)
    /// </summary>
    public DateTimeOffset? RegisteredToUtc { get; init; }
}
