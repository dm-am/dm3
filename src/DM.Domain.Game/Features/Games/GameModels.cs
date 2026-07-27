using System;
using System.Collections.Generic;
using DM.Domain.Core.Comments;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Likes;

namespace DM.Domain.Game.Features.Games;

/// <summary>
/// DTO model for game tag
/// </summary>
public class GameTag
{
    /// <summary>
    /// Internal identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Short numeric identifier for URL filtering (1, 2, 3...)
    /// </summary>
    public int ShortId { get; set; }

    /// <summary>
    /// Tag group title
    /// </summary>
    public string GroupTitle { get; set; } = null!;

    /// <summary>
    /// Tag group description
    /// </summary>
    public string? GroupDescription { get; set; }

    /// <summary>
    /// Tag group sort order
    /// </summary>
    public int GroupSortOrder { get; set; }

    /// <summary>
    /// Tag title
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// Tag description (may contain [tipimg:URL]text[/tipimg] for inline image tooltips)
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Tag sort order within group
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// Number of active games with this tag
    /// </summary>
    public int GamesCount { get; set; }
}

/// <summary>
/// DTO for game recruitment information
/// </summary>
public class GameRecruitment
{
    /// <summary>
    /// Recruitment is open for new players
    /// </summary>
    public bool IsOpen { get; set; }

    /// <summary>
    /// Maximum number of player characters allowed (null = unlimited)
    /// </summary>
    public int? PcLimit { get; set; }

    /// <summary>
    /// Current number of active player characters
    /// </summary>
    public int PcCount { get; set; }

    /// <summary>
    /// When the recruitment was started
    /// </summary>
    public DateTimeOffset? StartedUtc { get; set; }

    /// <summary>
    /// Whether this is a subsequent recruitment ("донабор", RecruitmentCount >= 2)
    /// </summary>
    public bool IsSubsequent { get; set; }
}

/// <summary>
/// Lightweight assistant info for game lists and tooltips
/// </summary>
public class GameAssistantInfo
{
    /// <summary>
    /// User identifier
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Username for display
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// When the assistant joined the game
    /// </summary>
    public DateTimeOffset JoinedUtc { get; set; }

    /// <summary>
    /// Last activity moment (UTC) - for online indicators
    /// </summary>
    public DateTimeOffset? LastActivityUtc { get; set; }

    /// <summary>
    /// User role (for displaying role badges [А], [С], [М], [Н], [Р])
    /// </summary>
    public UserRole Role { get; set; }

    /// <summary>
    /// Whether user is a newbie (less than 100 posts) - affects name color
    /// </summary>
    public bool IsNewbie { get; set; }
}

/// <summary>
/// Blacklisted user
/// </summary>
public class BlacklistedUser
{
    /// <summary>
    /// User identifier
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Link identifier
    /// </summary>
    public Guid LinkId { get; set; }
}

/// <summary>
/// Post pendency (who is expected to post in a room)
/// </summary>
public class PostPendency
{
    /// <summary>
    /// Identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Room identifier
    /// </summary>
    public Guid RoomId { get; set; }

    /// <summary>
    /// Character identifier (whose turn it is to post)
    /// </summary>
    public Guid CharacterId { get; set; }

    /// <summary>
    /// Who created this expectation
    /// </summary>
    public GeneralUser CreatedBy { get; set; } = null!;

    /// <summary>
    /// Who is expected to write the post
    /// </summary>
    public GeneralUser WaitingForUser { get; set; } = null!;

    /// <summary>
    /// When the expectation was created
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// When the expectation was fulfilled
    /// </summary>
    public DateTimeOffset? FulfilledUtc { get; set; }
}

/// <summary>
/// DTO model for game (lightweight, for lists)
/// </summary>
public class Game
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
    /// Created date (UTC)
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Game status
    /// </summary>
    public ModuleStatus Status { get; set; }

    /// <summary>
    /// Premoderation status for newbie GMs
    /// </summary>
    public PremoderationStatus PremoderationStatus { get; set; }

    /// <summary>
    /// Reason why the game was closed (only applicable when Status = Closed)
    /// </summary>
    public ClosedReason ClosedReason { get; set; }

    /// <summary>
    /// Visibility of draft content (when Status = Draft)
    /// </summary>
    public DraftVisibility DraftVisibility { get; set; }

    /// <summary>
    /// Recruitment information
    /// </summary>
    public GameRecruitment Recruitment { get; set; } = null!;

    /// <summary>
    /// When the game was closed
    /// </summary>
    public DateTimeOffset? ClosedUtc { get; set; }

    /// <summary>
    /// Game tags (full objects - only for single game details, null for lists)
    /// </summary>
    public IEnumerable<GameTag>? Tags { get; set; }

    /// <summary>
    /// Tag IDs only (lightweight - for lists, sidebar lookup)
    /// </summary>
    public IEnumerable<int> TagIds { get; set; } = [];

    /// <summary>
    /// Date the game was first activated (UTC)
    /// </summary>
    public DateTimeOffset? ActivatedUtc { get; set; }

    /// <summary>
    /// Attribute schema identifier
    /// </summary>
    public Guid? AttributeSchemaId { get; set; }

    /// <summary>
    /// Game master (GM)
    /// </summary>
    public GeneralUser Master { get; set; } = null!;

    /// <summary>
    /// Game master's assistants (lightweight info for lists and tooltips)
    /// </summary>
    public IEnumerable<GameAssistantInfo> Assistants { get; set; } = [];

    /// <summary>
    /// Game premoderation moderator (null if not in moderation)
    /// </summary>
    public GeneralUser? Mentor { get; set; }

    /// <summary>
    /// Pending assistant if any
    /// </summary>
    public GeneralUser? PendingAssistant { get; set; }

    /// <summary>
    /// Unique players (authors of active characters) - for game details page
    /// </summary>
    public IEnumerable<GeneralUser> Players { get; set; } = [];

    /// <summary>
    /// Game subscriber ids
    /// </summary>
    public IEnumerable<Guid> SubscriberIds { get; set; } = [];

    /// <summary>
    /// User ids with pending invitations (player or reader)
    /// </summary>
    public IEnumerable<Guid> PendingInvitedUserIds { get; set; } = [];

    /// <summary>
    /// User ids with pending PLAYER invitations specifically
    /// </summary>
    public IEnumerable<Guid> PendingPlayerInvitedUserIds { get; set; } = [];

    /// <summary>
    /// Blacklisted user ids
    /// </summary>
    public IEnumerable<BlacklistedUser> BlacklistedUsers { get; set; } = [];

    /// <summary>
    /// Game post pendencies
    /// </summary>
    public IEnumerable<PostPendency> Pendencies { get; set; } = [];

    /// <summary>
    /// Game title
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// Tag description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Game RPG system
    /// </summary>
    public string SystemName { get; set; } = null!;

    /// <summary>
    /// Narrative setting (e.g. Mass Effect, Warhammer, Our world)
    /// </summary>
    public string NarrativeSetting { get; set; } = null!;

    /// <summary>
    /// comments access mode
    /// </summary>
    public CommentsAccessMode CommentsAccessMode { get; set; }

    /// <summary>
    /// Total comments count
    /// </summary>
    public int CommentCount { get; set; }

    /// <summary>
    /// Last comment identifier for navigation
    /// </summary>
    public Guid? LastCommentId { get; set; }

    /// <summary>
    /// Number of unread game posts
    /// </summary>
    public int UnreadPostsCount { get; set; }

    /// <summary>
    /// Number of unread game comments
    /// </summary>
    public int UnreadCommentsCount { get; set; }

    /// <summary>
    /// Number of unread characters in game
    /// </summary>
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
    /// Subscriber usernames for tooltip display (limited to first 20)
    /// </summary>
    public IEnumerable<string> SubscriberUsernames { get; set; } = [];

    /// <summary>
    /// Active characters info for [X/Y] tooltip display
    /// </summary>
    public IEnumerable<ActiveCharacterInfo> ActiveCharacters { get; set; } = [];

    /// <summary>
    /// Characters owned by the player targeted by <see cref="GamesQuery.PlayerUsername"/>.
    /// Null when the list query had no player filter (only the filtered list path
    /// hydrates this - see GameRepository.GetGames).
    /// </summary>
    public IEnumerable<PlayerCharacterInfo>? FilteredPlayerCharacters { get; set; }
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

/// <summary>
/// Character info of the player targeted by the games list player filter
/// (name + status for the profile games table)
/// </summary>
public class PlayerCharacterInfo
{
    /// <summary>
    /// Character name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Character status
    /// </summary>
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

/// <summary>
/// DTO model for room settings
/// </summary>
public class RoomSettings
{
    /// <summary>
    /// Any user can read private messages within posts
    /// </summary>
    public bool ViewPrivateText { get; set; }

    /// <summary>
    /// Any user can see dice roll results
    /// </summary>
    public bool ViewDiceResults { get; set; }

    /// <summary>
    /// Dice rolling is enabled in this room
    /// </summary>
    public bool DiceEnabled { get; set; }
}

/// <summary>
/// DTO model for room access
/// </summary>
public class RoomAccess
{
    /// <summary>
    /// Identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Room identifier
    /// </summary>
    public Guid RoomId { get; set; }

    /// <summary>
    /// Type of access target (Character or Reader)
    /// </summary>
    public RoomAccessTargetType TargetType { get; set; }

    /// <summary>
    /// Character (when TargetType = Character)
    /// </summary>
    public Character Character { get; set; } = null!;

    /// <summary>
    /// User who has access (reader or character author)
    /// </summary>
    public GeneralUser User { get; set; } = null!;

    /// <summary>
    /// When access was granted
    /// </summary>
    public DateTimeOffset GrantedUtc { get; set; }

    /// <summary>
    /// User who granted access
    /// </summary>
    public GeneralUser GrantedBy { get; set; } = null!;
}

/// <summary>
/// DTO Model for game room
/// </summary>
public class Room
{
    /// <summary>
    /// Identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Room number within game (for URL, stable)
    /// </summary>
    public int RoomNumber { get; set; }

    /// <summary>
    /// Game identifier
    /// </summary>
    public Guid GameId { get; set; }

    /// <summary>
    /// Linked chat identifier (for RoomType.Chat rooms)
    /// </summary>
    public Guid? ChatId { get; set; }

    /// <summary>
    /// Room title
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// Tag description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Room content type
    /// </summary>
    public RoomType Type { get; set; }

    /// <summary>
    /// Room access type (visibility)
    /// </summary>
    public RoomAccessType AccessType { get; set; }

    /// <summary>
    /// Room is archived (hidden from the active rooms list, kept for history)
    /// </summary>
    public bool IsArchived { get; set; }

    /// <summary>
    /// Room access links (characters and readers)
    /// </summary>
    public IEnumerable<RoomAccess> Accesses { get; set; } = [];

    /// <summary>
    /// Post pendencies
    /// </summary>
    public IEnumerable<PostPendency> Pendencies { get; set; } = [];

    /// <summary>
    /// Total room posts count
    /// </summary>
    public int TotalPostsCount { get; set; }

    /// <summary>
    /// Unread posts count
    /// </summary>
    public int UnreadPostsCount { get; set; }

    /// <summary>
    /// Room order number
    /// </summary>
    public double OrderNumber { get; set; }

    /// <summary>
    /// Previous room identifier
    /// </summary>
    public Guid? PreviousRoomId { get; set; }

    /// <summary>
    /// Room settings
    /// </summary>
    public RoomSettings Settings { get; set; } = null!;

    /// <summary>
    /// Default room name
    /// </summary>
    public const string DefaultRoomName = "Игровая комната";
}

/// <summary>
/// Room reference embedded in post listings. Carries a nested
/// <see cref="Game"/> instead of scalar game id/title fields so
/// downstream tooltips (GameLink / RoomLink) receive the same full
/// <c>GameRef</c>-tier payload the sidebar already uses — master,
/// assistants, active characters, recruitment, subscriber counts —
/// without a second HTTP round-trip per post. Populated by
/// <c>PostRepository.GetRated</c>'s batch game-hydration step.
/// </summary>
public class RoomRef
{
    /// <summary>
    /// Room identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Room number within game (for URL)
    /// </summary>
    public int RoomNumber { get; set; }

    /// <summary>
    /// Room title
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// Parent game — full GameRef-tier payload populated after
    /// pagination via a batched hydration call. Null only for
    /// orphaned posts or posts whose parent game has been removed.
    /// </summary>
    public Game? Game { get; set; }
}

/// <summary>
/// Shortened character info
/// </summary>
public class CharacterShort
{
    /// <summary>
    /// Identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Character owner
    /// </summary>
    public GeneralUser Author { get; set; } = null!;

    /// <summary>
    /// Character status
    /// </summary>
    public CharacterStatus Status { get; set; }

    /// <summary>
    /// Character name
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// Character avatar (3 variants). Null URLs if no avatar is uploaded.
    /// Symmetric with <see cref="GeneralUser.Picture"/>.
    /// </summary>
    public AvatarPicture Picture { get; set; } = new();

    /// <summary>
    /// Character is NPC
    /// </summary>
    public bool IsNpc { get; set; }

    /// <summary>
    /// GM access policy
    /// </summary>
    public CharacterAccessPolicy AccessPolicy { get; set; }
}

/// <summary>
/// Character attribute value
/// </summary>
public class CharacterAttribute
{
    /// <summary>
    /// Value identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Attribute specification identifier
    /// </summary>
    public Guid AttributeId { get; set; }

    /// <summary>
    /// Attribute title
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// Tag description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Attribute value
    /// </summary>
    public string Value { get; set; } = null!;

    /// <summary>
    /// Attribute modifier
    /// </summary>
    public int? Modifier { get; set; }

    /// <summary>
    /// Constraint type of the backing specification. Set by the value filler
    /// so the API layer can server-render <see cref="AttributeSpecificationType.BbCode"/>
    /// values instead of emitting the raw stored BBCode string.
    /// </summary>
    public AttributeSpecificationType Type { get; set; }

    /// <summary>
    /// Value is inconsistent with specification
    /// </summary>
    public bool Inconsistent { get; set; }
}

/// <summary>
/// DTO model for game character
/// </summary>
public class Character
{
    /// <summary>
    /// Character identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Game identifier
    /// </summary>
    public Guid GameId { get; set; }

    /// <summary>
    /// Created date (UTC)
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Last modification date (UTC)
    /// </summary>
    public DateTimeOffset? ModifiedUtc { get; set; }

    /// <summary>
    /// Character status
    /// </summary>
    public CharacterStatus Status { get; set; }

    /// <summary>
    /// Character is dead
    /// </summary>
    public bool IsDead { get; set; }

    /// <summary>
    /// Player has voluntarily left the game
    /// </summary>
    public bool IsPlayerLeft { get; set; }

    /// <summary>
    /// Player has been exiled from the game by master
    /// </summary>
    public bool IsPlayerExiled { get; set; }

    /// <summary>
    /// Total characters posts count
    /// </summary>
    public int TotalPostsCount { get; set; }

    /// <summary>
    /// Timestamp of the character's most recent post (null if it never posted).
    /// Projected as an aggregate subquery, not a collection join.
    /// </summary>
    public DateTimeOffset? LastPostUtc { get; set; }

    /// <summary>
    /// Value of the character's descriptor attribute ("Класс" on the game main
    /// page). Populated by CharacterAttributeValueFiller from the schema's
    /// descriptor specification; null when the schema has no descriptor spec or
    /// the character has no value for it.
    /// </summary>
    public string? Descriptor { get; set; }

    /// <summary>
    /// Character author
    /// </summary>
    public GeneralUser Author { get; set; } = null!;

    /// <summary>
    /// Character name
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// Character avatar (3 variants). Null URLs if no avatar is uploaded.
    /// Symmetric with <see cref="GeneralUser.Picture"/>.
    /// </summary>
    public AvatarPicture Picture { get; set; } = new();

    /// <summary>
    /// Character is NPC (non-player's character)
    /// </summary>
    public bool IsNpc { get; set; }

    /// <summary>
    /// GM access policy
    /// </summary>
    public CharacterAccessPolicy AccessPolicy { get; set; }

    /// <summary>
    /// Character attribute
    /// </summary>
    public IEnumerable<CharacterAttribute> Attributes { get; set; } = [];
}

/// <summary>
/// Short character info for game listing
/// </summary>
public class CharacterShortInfo : CharacterShort
{
    /// <summary>
    /// Last post info
    /// </summary>
    public LastPost? LastPost { get; set; }

    /// <summary>
    /// Total posts count
    /// </summary>
    public int PostsCount { get; set; }
}

/// <summary>
/// Last post info
/// </summary>
public class LastPost
{
    /// <summary>
    /// Post identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Room identifier
    /// </summary>
    public Guid RoomId { get; set; }

    /// <summary>
    /// Created timestamp
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }
}

/// <summary>
/// Attribute list value
/// </summary>
public class ListValue
{
    /// <summary>
    /// Value
    /// </summary>
    public string Value { get; set; } = null!;

    /// <summary>
    /// Modifier
    /// </summary>
    public int? Modifier { get; set; }
}

/// <summary>
/// DTO model for game post
/// </summary>
public class Post
{
    /// <summary>
    /// Identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Room identifier
    /// </summary>
    public Guid RoomId { get; set; }

    /// <summary>
    /// Post author
    /// </summary>
    public GeneralUser Author { get; set; } = null!;

    /// <summary>
    /// Short character information
    /// </summary>
    public CharacterShort Character { get; set; } = null!;

    /// <summary>
    /// Creating moment (UTC)
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Game text (in-character content)
    /// </summary>
    public string GameText { get; set; } = null!;

    /// <summary>
    /// Metagame text (OOC commentary)
    /// </summary>
    public string MetagameText { get; set; } = null!;

    /// <summary>
    /// Sum of review scores
    /// </summary>
    public int Rating { get; set; }

    /// <summary>
    /// Number of reviews
    /// </summary>
    public int ReviewCount { get; set; }

    /// <summary>
    /// Author role in game context (DungeonMaster/Assistant/null for player)
    /// </summary>
    public string? AuthorGameRole { get; set; }

    /// <summary>
    /// Room reference (for rated posts listing)
    /// </summary>
    public RoomRef? Room { get; set; }

    /// <summary>
    /// Dice rolls associated with this post
    /// </summary>
    public IEnumerable<Posts.DiceRoll> DiceRolls { get; set; } = [];

    /// <summary>
    /// Edit history (most recent first)
    /// </summary>
    public IEnumerable<Posts.PostEdit> Edits { get; set; } = [];

    /// <summary>
    /// Author user id (for render-context envelope: author-forever rule).
    /// </summary>
    public Guid AuthorUserId { get; set; }

    /// <summary>
    /// Game identifier the post belongs to.
    /// </summary>
    public Guid GameId { get; set; }

    /// <summary>
    /// Per-post override opening [private] to every room reader.
    /// </summary>
    public bool SharePrivateWithAll { get; set; }

    /// <summary>
    /// Per-room override opening [private] to every room reader.
    /// Populated from Room.ViewPrivateText at read time.
    /// </summary>
    public bool RoomViewPrivateText { get; set; }

    /// <summary>
    /// Game leads (master + assistants) at read time. Mentors are NOT
    /// included — a mentor is not a lead.
    /// </summary>
    public IReadOnlyCollection<Guid> GameLeadUserIds { get; set; } = [];

    /// <summary>
    /// Raw JSONB snapshot of owner user ids for every [private] block in
    /// <see cref="GameText"/>. Parsed by the API layer (ProjectTo cannot
    /// deserialize JSON server-side). Shape: <c>{ "AddresseeAttrValue":
    /// ["guid", "guid", ...], ... }</c>.
    /// </summary>
    public string PrivateAddresseeSnapshotJson { get; set; } = "{}";
}

/// <summary>
/// Attribute specification
/// </summary>
public class AttributeSpecification
{
    /// <summary>
    /// Identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Title
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// Tag description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Order number
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Attribute type
    /// </summary>
    public AttributeSpecificationType Type { get; set; }

    /// <summary>
    /// Is required
    /// </summary>
    public bool Required { get; set; }

    /// <summary>
    /// Maximum length (for text/number/BBCode types)
    /// </summary>
    public int? MaxLength { get; set; }

    /// <summary>
    /// Available values (for list/string types)
    /// </summary>
    public IEnumerable<ListValue> Values { get; set; } = [];

    /// <summary>
    /// Is descriptor attribute
    /// </summary>
    public bool IsDescriptor { get; set; }

    /// <summary>
    /// Is hidden attribute
    /// </summary>
    public bool IsHidden { get; set; }
}

/// <summary>
/// Attribute schema
/// </summary>
public class AttributeSchema
{
    /// <summary>
    /// Identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Title
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// Tag description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Type
    /// </summary>
    public SchemaType Type { get; set; }

    /// <summary>
    /// Author (null for system schemas)
    /// </summary>
    public GeneralUser? Author { get; set; }

    /// <summary>
    /// Attribute specifications
    /// </summary>
    public IEnumerable<AttributeSpecification> Specifications { get; set; } = [];
}

/// <summary>
/// Extended DTO model for game (full details, for detail pages)
/// </summary>
public class GameDetails : Game
{
    /// <summary>
    /// Game rooms
    /// </summary>
    public IEnumerable<Room> Rooms { get; set; } = [];

    /// <summary>
    /// Game subscribers
    /// </summary>
    public IEnumerable<GeneralUser> Subscribers { get; set; } = [];

    /// <summary>
    /// Full assistant information for detail page
    /// </summary>
    public IEnumerable<GeneralUser> FullAssistants { get; set; } = [];

    /// <summary>
    /// Game characters
    /// </summary>
    public IEnumerable<CharacterShortInfo> Characters { get; set; } = [];

    /// <summary>
    /// Game public information
    /// </summary>
    public string Info { get; set; } = null!;

    /// <summary>
    /// Game private master information
    /// </summary>
    public string Notepad { get; set; } = null!;

    /// <summary>
    /// Only GM and post author can see dice roll result
    /// </summary>
    public bool HideDiceResult { get; set; }

    /// <summary>
    /// Any user can read private messages within posts
    /// </summary>
    public bool ShowPrivateMessages { get; set; }

    /// <summary>
    /// Hide posts count and last post date for all users (except Master/Assistant)
    /// </summary>
    public bool HidePostStats { get; set; }

    /// <summary>
    /// Attribute schema details
    /// </summary>
    public AttributeSchema AttributeSchema { get; set; } = null!;

    /// <summary>
    /// Total number of posts across all rooms of the game ("Постов всего").
    /// Computed with a batched aggregate query on the details read path.
    /// </summary>
    public int TotalPostsCount { get; set; }

    /// <summary>
    /// Number of posts authored by the game master ("Постов мастера").
    /// Computed with a batched aggregate query on the details read path.
    /// </summary>
    public int MasterPostsCount { get; set; }

    /// <summary>
    /// Timestamp of the game master's most recent post (null if none).
    /// </summary>
    public DateTimeOffset? LastMasterPostUtc { get; set; }

    /// <summary>
    /// Whether any room of the game has dice rolling enabled ("Поддержка кубика").
    /// </summary>
    public bool DiceSupported { get; set; }
}

/// <summary>
/// Game invitation
/// </summary>
public class GameInvitation
{
    /// <summary>
    /// Token identifier
    /// </summary>
    public Guid TokenId { get; set; }

    /// <summary>
    /// Game identifier
    /// </summary>
    public Guid GameId { get; set; }

    /// <summary>
    /// Game title
    /// </summary>
    public string GameTitle { get; set; } = null!;

    /// <summary>
    /// Invited user
    /// </summary>
    public GeneralUser InvitedUser { get; set; } = null!;

    /// <summary>
    /// User who sent the invitation
    /// </summary>
    public GeneralUser InvitedBy { get; set; } = null!;

    /// <summary>
    /// Target role
    /// </summary>
    public GameRole TargetRole { get; set; }

    /// <summary>
    /// When the invitation was created
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// When the invitation expires
    /// </summary>
    public DateTimeOffset ExpiresUtc { get; set; }
}

/// <summary>
/// Game comment
/// </summary>
public class GameComment : ILikable
{
    /// <summary>
    /// Comment identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Game identifier
    /// </summary>
    public Guid GameId { get; set; }

    /// <summary>
    /// Author
    /// </summary>
    public GeneralUser Author { get; set; } = null!;

    /// <summary>
    /// Comment text
    /// </summary>
    public string Text { get; set; } = null!;

    /// <summary>
    /// Created date
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Modified date
    /// </summary>
    public DateTimeOffset? ModifiedUtc { get; set; }

    /// <summary>
    /// Likes count
    /// </summary>
    public int LikesCount { get; set; }

    /// <summary>
    /// Users who liked this comment
    /// </summary>
    public IEnumerable<GeneralUser> Likes { get; set; } = [];

    /// <summary>
    /// Like entity type
    /// </summary>
    public LikeEntityType LikeEntityType => LikeEntityType.Comment;
}

/// <summary>
/// Game user with their role
/// </summary>
public class GameUser
{
    /// <summary>
    /// User information
    /// </summary>
    public GeneralUser User { get; set; } = null!;

    /// <summary>
    /// User's role in the game
    /// </summary>
    public GameRole Role { get; set; }

    /// <summary>
    /// When the user joined the game
    /// </summary>
    public DateTimeOffset JoinedUtc { get; set; }

    /// <summary>
    /// Character ID (for players)
    /// </summary>
    public Guid? CharacterId { get; set; }

    /// <summary>
    /// Character name (for players)
    /// </summary>
    public string? CharacterName { get; set; }

    /// <summary>
    /// Character status (for players)
    /// </summary>
    public CharacterStatus? CharacterStatus { get; set; }
}

/// <summary>
/// DTO for game invitation token
/// </summary>
public class GameInvitationToken
{
    /// <summary>
    /// Token identifier
    /// </summary>
    public Guid TokenId { get; set; }

    /// <summary>
    /// User identifier
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Token type
    /// </summary>
    public TokenType TokenType { get; set; }

    /// <summary>
    /// Entity identifier (game ID)
    /// </summary>
    public Guid? EntityId { get; set; }

    /// <summary>
    /// Creation timestamp
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Whether the token is valid (not deleted)
    /// </summary>
    public bool IsValid { get; set; }
}

/// <summary>
/// Cancelled invitation result
/// </summary>
public class CancelledInvitation
{
    /// <summary>
    /// Token identifier
    /// </summary>
    public Guid TokenId { get; set; }

    /// <summary>
    /// Token type
    /// </summary>
    public TokenType TokenType { get; set; }
}

/// <summary>
/// Game comment delete info (extends Comment for authorization)
/// </summary>
public class GameCommentToDelete : Comment
{
    /// <summary>
    /// Comment creation date
    /// </summary>
    public new DateTimeOffset CreatedUtc
    {
        get => base.CreatedUtc;
        set => base.CreatedUtc = value;
    }

    /// <summary>
    /// Game identifier
    /// </summary>
    public Guid GameId { get; set; }

    /// <summary>
    /// Current game comment count
    /// </summary>
    public int GameCommentCount { get; set; }

    /// <summary>
    /// Whether this is the last comment
    /// </summary>
    public bool IsLastComment { get; set; }
}

/// <summary>
/// Room info for reordering
/// </summary>
public class RoomOrderInfo
{
    /// <summary>
    /// Room identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Order number
    /// </summary>
    public double OrderNumber { get; set; }
}

/// <summary>
/// Room neighbours for reordering
/// </summary>
public class RoomNeighbours
{
    /// <summary>
    /// Previous room
    /// </summary>
    public RoomOrderInfo? Previous { get; set; }

    /// <summary>
    /// Current room
    /// </summary>
    public RoomOrderInfo Current { get; set; } = null!;

    /// <summary>
    /// Next room
    /// </summary>
    public RoomOrderInfo? Next { get; set; }
}

/// <summary>
/// Room for update (extends Room with parent game reference)
/// </summary>
public class RoomToUpdate : Room
{
    /// <summary>
    /// Parent game (for authorization checks)
    /// </summary>
    public Game Game { get; set; } = null!;
}

/// <summary>
/// Character for update
/// </summary>
public class CharacterToUpdate
{
    /// <summary>
    /// Character identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Game identifier
    /// </summary>
    public Guid GameId { get; set; }

    /// <summary>
    /// Author user identifier
    /// </summary>
    public Guid AuthorId { get; set; }

    /// <summary>
    /// Character author user identifier (alias for AuthorId for clarity)
    /// </summary>
    public Guid UserId => AuthorId;

    /// <summary>
    /// Game master identifier
    /// </summary>
    public Guid GameMasterId { get; set; }

    /// <summary>
    /// Game assistant identifiers
    /// </summary>
    public IEnumerable<Guid> GameAssistantIds { get; set; } = [];

    /// <summary>
    /// Game status
    /// </summary>
    public ModuleStatus GameStatus { get; set; }

    /// <summary>
    /// Character status
    /// </summary>
    public CharacterStatus Status { get; set; }

    /// <summary>
    /// Whether the character was modified
    /// </summary>
    public bool IsModified { get; set; }

    /// <summary>
    /// Character is NPC
    /// </summary>
    public bool IsNpc { get; set; }

    /// <summary>
    /// GM access policy
    /// </summary>
    public CharacterAccessPolicy AccessPolicy { get; set; }

    /// <summary>
    /// Character is dead
    /// </summary>
    public bool IsDead { get; set; }

    /// <summary>
    /// Player has voluntarily left the game
    /// </summary>
    public bool IsPlayerLeft { get; set; }

    /// <summary>
    /// Created timestamp
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }
}
