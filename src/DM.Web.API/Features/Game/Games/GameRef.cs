using System;
using System.Collections.Generic;
using DM.Domain.Core.Enums;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Game.Games;

/// <summary>
/// Lightweight game reference for sidebars and menus.
/// Unlike full Game, uses counts instead of user arrays for players/readers.
/// </summary>
public class GameRef
{
    /// <summary>
    /// Game identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Public identifier for URLs (5 lowercase letters)
    /// </summary>
    public string PublicId { get; set; } = null!;

    /// <summary>
    /// Game title
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// Game status
    /// </summary>
    public ModuleStatus? Status { get; set; }

    /// <summary>
    /// Closed reason (for Closed status)
    /// </summary>
    public ClosedReason? ClosedReason { get; set; }

    /// <summary>
    /// Game first activation date
    /// </summary>
    public DateTimeOffset? ActivatedUtc { get; set; }

    /// <summary>
    /// Game master (lightweight reference)
    /// </summary>
    public UserRef Master { get; set; } = null!;

    /// <summary>
    /// Game master's assistants (lightweight references for tooltip)
    /// </summary>
    public IEnumerable<UserRef> Assistants { get; set; } = [];

    /// <summary>
    /// Requesting user participation flags
    /// </summary>
    public IEnumerable<GameParticipation> Participation { get; set; } = [];

    /// <summary>
    /// Game waits for a post from the requesting user
    /// </summary>
    /// <remarks>
    /// The same expectations the room list marks with a star, summed up for the
    /// whole game, so a list of games can show the marker without reading every
    /// room. False for an anonymous request.
    /// </remarks>
    public bool AwaitsViewerTurn { get; set; }

    /// <summary>
    /// Names of the requesting user's characters the game waits a post for,
    /// oldest expectation first. Empty unless <see cref="AwaitsViewerTurn" />.
    /// </summary>
    public IEnumerable<string> AwaitedCharacterNames { get; set; } = [];

    /// <summary>
    /// Number of subscribers (readers)
    /// </summary>
    public int SubscribersCount { get; set; }

    /// <summary>
    /// Recruitment information
    /// </summary>
    public GameRecruitment Recruitment { get; set; } = null!;

    /// <summary>
    /// Number of unread posts
    /// </summary>
    public int UnreadPostsCount { get; set; }

    /// <summary>
    /// Number of unread commentaries
    /// </summary>
    public int UnreadCommentsCount { get; set; }

    /// <summary>
    /// Number of reviews about the game itself
    /// </summary>
    public int GameReviewsCount { get; set; }

    /// <summary>
    /// Number of reviews about posts in the game
    /// </summary>
    public int PostReviewsCount { get; set; }

    /// <summary>
    /// Subscriber usernames for tooltip display: at most 20, the most recently
    /// active first. <see cref="SubscribersCount" /> is the real total.
    /// </summary>
    public IEnumerable<string> SubscriberUsernames { get; set; } = [];

    /// <summary>
    /// Active characters info for [X/Y] tooltip display
    /// </summary>
    public IEnumerable<ActiveCharacterInfo> ActiveCharacters { get; set; } = [];
}

/// <summary>
/// Active character info for tooltip display
/// </summary>
public class ActiveCharacterInfo
{
    /// <summary>
    /// Character name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Owner's username
    /// </summary>
    public string OwnerUsername { get; set; } = string.Empty;
}
