using System;
using System.Collections.Generic;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.Blacklists;
using DM.Domain.Game.Features.PostPendencies;

namespace DM.Domain.Game.Features.Games;

public class GameRecruitment
{
    public bool IsOpen { get; set; }

    /// <summary>
    /// Null is unlimited
    /// </summary>
    public int? PcLimit { get; set; }

    /// <summary>
    /// Current number of active player characters
    /// </summary>
    public int PcCount { get; set; }

    public DateTimeOffset? StartedUtc { get; set; }

    /// <summary>
    /// A recruitment after the first ("донабор")
    /// </summary>
    public bool IsSubsequent { get; set; }
}

public class GameAssistantInfo
{
    public Guid UserId { get; set; }

    public string Username { get; set; } = string.Empty;

    public DateTimeOffset JoinedUtc { get; set; }

    public DateTimeOffset? LastActivityUtc { get; set; }

    public UserRole Role { get; set; }

    public bool IsNewbie { get; set; }
}

public class Game
{
    public Guid Id { get; set; }

    /// <summary>
    /// Public identifier for URLs
    /// </summary>
    public string PublicId { get; set; } = null!;

    public DateTimeOffset CreatedUtc { get; set; }

    public ModuleStatus Status { get; set; }

    /// <summary>
    /// Premoderation status for newbie GMs
    /// </summary>
    public PremoderationStatus PremoderationStatus { get; set; }

    /// <summary>
    /// When Status is Closed
    /// </summary>
    public ClosedReason ClosedReason { get; set; }

    /// <summary>
    /// When Status is Draft
    /// </summary>
    public DraftVisibility DraftVisibility { get; set; }

    public GameRecruitment Recruitment { get; set; } = null!;

    public DateTimeOffset? ClosedUtc { get; set; }

    /// <summary>
    /// Null in lists
    /// </summary>
    public IEnumerable<GameTag>? Tags { get; set; }

    public IEnumerable<int> TagIds { get; set; } = [];

    /// <summary>
    /// When the game was first activated
    /// </summary>
    public DateTimeOffset? ActivatedUtc { get; set; }

    public GeneralUser Master { get; set; } = null!;

    public IEnumerable<GameAssistantInfo> Assistants { get; set; } = [];

    /// <summary>
    /// Game premoderation moderator (null if not in moderation)
    /// </summary>
    public GeneralUser? Mentor { get; set; }

    public GeneralUser? PendingAssistant { get; set; }

    /// <summary>
    /// Authors of active characters
    /// </summary>
    public IEnumerable<GeneralUser> Players { get; set; } = [];

    public int SubscribersCount { get; set; }

    /// <summary>
    /// Whether the user this game was read for subscribes to it.
    /// </summary>
    /// <remarks>
    /// Viewer-scoped, so <c>GetRoles</c> answers <see cref="GameRole.Reader" /> only for that same user.
    /// </remarks>
    public bool IsViewerSubscriber { get; set; }

    /// <summary>
    /// User ids with pending invitations (player or reader)
    /// </summary>
    public IEnumerable<Guid> PendingInvitedUserIds { get; set; } = [];

    /// <summary>
    /// User ids with pending PLAYER invitations specifically
    /// </summary>
    public IEnumerable<Guid> PendingPlayerInvitedUserIds { get; set; } = [];

    public IEnumerable<BlacklistedUser> BlacklistedUsers { get; set; } = [];

    public IEnumerable<PostPendency> Pendencies { get; set; } = [];

    /// <summary>
    /// Whether the game is waiting for a post from the user this game was read
    /// for.
    /// </summary>
    /// <remarks>
    /// Viewer-scoped, from the expectations the room list draws its star from; false for a guest.
    /// </remarks>
    public bool AwaitsViewerTurn { get; set; }

    /// <summary>
    /// Names of that viewer's characters the game is waiting a post for, oldest
    /// expectation first. Empty whenever <see cref="AwaitsViewerTurn" /> is false.
    /// </summary>
    public IEnumerable<string> AwaitedCharacterNames { get; set; } = [];

    public string Title { get; set; } = null!;

    public string SystemName { get; set; } = null!;

    /// <summary>
    /// Narrative setting (e.g. Mass Effect, Warhammer, Our world)
    /// </summary>
    public string NarrativeSetting { get; set; } = null!;

    /// <summary>
    /// Who may write in the discussion; everybody who sees the game reads it
    /// </summary>
    public CommentsAccessMode CommentsAccessMode { get; set; }

    public int CommentCount { get; set; }

    public Guid? LastCommentId { get; set; }

    public int UnreadPostsCount { get; set; }

    public int UnreadCommentsCount { get; set; }

    public int UnreadCharactersCount { get; set; }

    /// <summary>
    /// Number of reviews about the game itself
    /// </summary>
    public int GameReviewsCount { get; set; }

    /// <summary>
    /// Number of reviews about posts in the game
    /// </summary>
    public int PostReviewsCount { get; set; }

    /// <summary>
    /// Subscriber usernames for tooltip display, capped and ordered per
    /// <see cref="SubscriptionPolicy.PreviewCap" />
    /// </summary>
    public IEnumerable<string> SubscriberUsernames { get; set; } = [];

    public IEnumerable<ActiveCharacterInfo> ActiveCharacters { get; set; } = [];

    /// <summary>
    /// Characters of the player <see cref="GamesQuery.PlayerUsername"/> names; null without that filter.
    /// </summary>
    public IEnumerable<PlayerCharacterInfo>? FilteredPlayerCharacters { get; set; }
}

public class ActiveCharacterInfo
{
    public string Name { get; set; } = string.Empty;

    public string OwnerUsername { get; set; } = string.Empty;
}

public class PlayerCharacterInfo
{
    public string Name { get; set; } = string.Empty;

    public CharacterStatus Status { get; set; }

    /// <summary>
    /// Character died in game (meaningful when Status = Retired)
    /// </summary>
    public bool IsDead { get; set; }

    /// <summary>
    /// Player voluntarily left the game (meaningful when Status = Retired)
    /// </summary>
    public bool IsPlayerLeft { get; set; }

    /// <summary>
    /// Player was exiled by the master (meaningful when Status = Retired)
    /// </summary>
    public bool IsPlayerExiled { get; set; }
}
