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
    /// Identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Tag group title
    /// </summary>
    public string GroupTitle { get; set; } = null!;

    /// <summary>
    /// Tag title
    /// </summary>
    public string Title { get; set; } = null!;

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
    /// Maximum number of players allowed (null = unlimited)
    /// </summary>
    public int? PlayerLimit { get; set; }

    /// <summary>
    /// Current number of active players
    /// </summary>
    public int PlayerCount { get; set; }

    /// <summary>
    /// When the recruitment was started
    /// </summary>
    public DateTimeOffset? StartedUtc { get; set; }
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
/// DTO model for game
/// </summary>
public class GameModel
{
    /// <summary>
    /// Game identifier
    /// </summary>
    public Guid Id { get; set; }

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
    /// Game was completed successfully (only when Status = Closed)
    /// </summary>
    public bool IsFinished { get; set; }

    /// <summary>
    /// Game was frozen due to inactivity (only when Status = Closed)
    /// </summary>
    public bool IsFrozen { get; set; }

    /// <summary>
    /// Recruitment information
    /// </summary>
    public GameRecruitment Recruitment { get; set; } = null!;

    /// <summary>
    /// When the game was closed
    /// </summary>
    public DateTimeOffset? ClosedUtc { get; set; }

    /// <summary>
    /// Game tags
    /// </summary>
    public IEnumerable<GameTag> Tags { get; set; } = [];

    /// <summary>
    /// Date the game was first released
    /// </summary>
    public DateTimeOffset? ReleaseDate { get; set; }

    /// <summary>
    /// Attribute schema identifier
    /// </summary>
    public Guid? AttributeSchemaId { get; set; }

    /// <summary>
    /// Game author (Master/GM)
    /// </summary>
    public GeneralUser Author { get; set; } = null!;

    /// <summary>
    /// Game master's assistants
    /// </summary>
    public IEnumerable<GeneralUser> Assistants { get; set; } = [];

    /// <summary>
    /// Game premoderation moderator (null if not in moderation)
    /// </summary>
    public GeneralUser? Mentor { get; set; }

    /// <summary>
    /// Pending assistant if any
    /// </summary>
    public GeneralUser? PendingAssistant { get; set; }

    /// <summary>
    /// Active game character author ids
    /// </summary>
    public IEnumerable<Guid> ActiveCharacterUserIds { get; set; } = [];

    /// <summary>
    /// Game reader ids
    /// </summary>
    public IEnumerable<Guid> ReaderUserIds { get; set; } = [];

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
    /// Game RPG system
    /// </summary>
    public string SystemName { get; set; } = null!;

    /// <summary>
    /// Narrative setting (e.g. Mass Effect, WarHammer, Our world)
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
    /// Game identifier
    /// </summary>
    public Guid GameId { get; set; }

    /// <summary>
    /// Room title
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// Room content type
    /// </summary>
    public RoomType Type { get; set; }

    /// <summary>
    /// Room access type (visibility)
    /// </summary>
    public RoomAccessType AccessType { get; set; }

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
    /// Character race
    /// </summary>
    public string Race { get; set; } = null!;

    /// <summary>
    /// Character class
    /// </summary>
    public string Class { get; set; } = null!;

    /// <summary>
    /// Picture URL
    /// </summary>
    public string PictureUrl { get; set; } = null!;

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
    /// Attribute value
    /// </summary>
    public string Value { get; set; } = null!;

    /// <summary>
    /// Attribute modifier
    /// </summary>
    public int? Modifier { get; set; }

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
    /// Character author
    /// </summary>
    public GeneralUser Author { get; set; } = null!;

    /// <summary>
    /// Character name
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// Character race
    /// </summary>
    public string Race { get; set; } = null!;

    /// <summary>
    /// Character class
    /// </summary>
    public string Class { get; set; } = null!;

    /// <summary>
    /// Character picture URL
    /// </summary>
    public string PictureUrl { get; set; } = null!;

    /// <summary>
    /// Character appearance
    /// </summary>
    public string Appearance { get; set; } = null!;

    /// <summary>
    /// Character temper
    /// </summary>
    public string Temper { get; set; } = null!;

    /// <summary>
    /// Character story
    /// </summary>
    public string Story { get; set; } = null!;

    /// <summary>
    /// Character skills
    /// </summary>
    public string Skills { get; set; } = null!;

    /// <summary>
    /// Character inventory
    /// </summary>
    public string Inventory { get; set; } = null!;

    /// <summary>
    /// Character alignment
    /// </summary>
    public Alignment? Alignment { get; set; }

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
public class Post : ILikable
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
    /// Post last update author
    /// </summary>
    public GeneralUser ModifiedBy { get; set; } = null!;

    /// <summary>
    /// Short character information
    /// </summary>
    public CharacterShort Character { get; set; } = null!;

    /// <summary>
    /// Creating moment (UTC)
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Last modification moment (UTC)
    /// </summary>
    public DateTimeOffset? ModifiedUtc { get; set; }

    /// <summary>
    /// Text
    /// </summary>
    public string Text { get; set; } = null!;

    /// <summary>
    /// Comment
    /// </summary>
    public string Comment { get; set; } = null!;

    /// <summary>
    /// Message to master or master note
    /// </summary>
    public string MasterMessage { get; set; } = null!;

    /// <summary>
    /// Likes count
    /// </summary>
    public int LikesCount { get; set; }

    /// <summary>
    /// Users who liked this post
    /// </summary>
    public IEnumerable<GeneralUser> Likes { get; set; } = [];

    /// <summary>
    /// Like entity type
    /// </summary>
    public LikeEntityType LikeEntityType => LikeEntityType.Post;
}

/// <summary>
/// Best post result
/// </summary>
public class BestPostResult
{
    /// <summary>
    /// Post identifier
    /// </summary>
    public Guid PostId { get; set; }

    /// <summary>
    /// Post text
    /// </summary>
    public string Text { get; set; } = null!;

    /// <summary>
    /// Game title
    /// </summary>
    public string GameTitle { get; set; } = null!;

    /// <summary>
    /// Game identifier
    /// </summary>
    public Guid GameId { get; set; }

    /// <summary>
    /// Room title
    /// </summary>
    public string RoomTitle { get; set; } = null!;

    /// <summary>
    /// Room identifier
    /// </summary>
    public Guid RoomId { get; set; }

    /// <summary>
    /// Author username
    /// </summary>
    public string AuthorUsername { get; set; } = null!;

    /// <summary>
    /// Post rating (sum of review signs)
    /// </summary>
    public int Rating { get; set; }

    /// <summary>
    /// Number of reviews
    /// </summary>
    public int ReviewCount { get; set; }

    /// <summary>
    /// Created timestamp
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }
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
    /// Order number
    /// </summary>
    public int OrderNumber { get; set; }

    /// <summary>
    /// Attribute type
    /// </summary>
    public AttributeSpecificationType Type { get; set; }

    /// <summary>
    /// Is required
    /// </summary>
    public bool Required { get; set; }

    /// <summary>
    /// Minimum value (for number/list types)
    /// </summary>
    public int? MinValue { get; set; }

    /// <summary>
    /// Maximum value (for number/list types)
    /// </summary>
    public int? MaxValue { get; set; }

    /// <summary>
    /// Maximum length (for string types)
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
    /// Type
    /// </summary>
    public SchemaType Type { get; set; }

    /// <summary>
    /// Author
    /// </summary>
    public GeneralUser Author { get; set; } = null!;

    /// <summary>
    /// Attribute specifications
    /// </summary>
    public IEnumerable<AttributeSpecification> Specifications { get; set; } = [];
}

/// <summary>
/// Extended DTO model for game
/// </summary>
public class GameExtended : GameModel
{
    /// <summary>
    /// Game rooms
    /// </summary>
    public IEnumerable<Room> Rooms { get; set; } = [];

    /// <summary>
    /// Game readers
    /// </summary>
    public IEnumerable<GeneralUser> Readers { get; set; } = [];

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
    /// Only GM and character author can see character temper
    /// </summary>
    public bool HideTemper { get; set; }

    /// <summary>
    /// Only GM and character author can see character skills
    /// </summary>
    public bool HideSkills { get; set; }

    /// <summary>
    /// Only GM and character author can see character inventory
    /// </summary>
    public bool HideInventory { get; set; }

    /// <summary>
    /// Only GM and character author can see character story
    /// </summary>
    public bool HideStory { get; set; }

    /// <summary>
    /// Only GM and post author can see dice roll result
    /// </summary>
    public bool HideDiceResult { get; set; }

    /// <summary>
    /// Disable character alignment
    /// </summary>
    public bool DisableAlignment { get; set; }

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
/// Featured post (best of week / last with plus)
/// </summary>
public class FeaturedPost
{
    /// <summary>
    /// Post identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Text preview
    /// </summary>
    public string TextPreview { get; set; } = null!;

    /// <summary>
    /// Post author
    /// </summary>
    public GeneralUser Author { get; set; } = null!;

    /// <summary>
    /// Character name
    /// </summary>
    public string? CharacterName { get; set; }

    /// <summary>
    /// Created date
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Game identifier
    /// </summary>
    public Guid GameId { get; set; }

    /// <summary>
    /// Game title
    /// </summary>
    public string GameTitle { get; set; } = null!;

    /// <summary>
    /// Room identifier
    /// </summary>
    public Guid RoomId { get; set; }

    /// <summary>
    /// Room title
    /// </summary>
    public string RoomTitle { get; set; } = null!;

    /// <summary>
    /// Post rating
    /// </summary>
    public int Rating { get; set; }

    /// <summary>
    /// Review count
    /// </summary>
    public int ReviewCount { get; set; }
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
    public GameModel Game { get; set; } = null!;
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
