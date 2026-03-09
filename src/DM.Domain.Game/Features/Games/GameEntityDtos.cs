using System;
using System.Collections.Generic;
using DM.Domain.Core.Enums;

namespace DM.Domain.Game.Features.Games;

/// <summary>
/// Entity DTO for creating a game (repository level)
/// </summary>
public class CreateGameEntity
{
    /// <summary>
    /// Game identifier
    /// </summary>
    public Guid GameId { get; set; }

    /// <summary>
    /// Author (master) user identifier
    /// </summary>
    public Guid AuthorId { get; set; }

    /// <summary>
    /// Game title
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// Game RPG system
    /// </summary>
    public string? SystemName { get; set; }

    /// <summary>
    /// Narrative setting
    /// </summary>
    public string? NarrativeSetting { get; set; }

    /// <summary>
    /// Game public information
    /// </summary>
    public string? Info { get; set; }

    /// <summary>
    /// Game status (Draft or Active)
    /// </summary>
    public ModuleStatus Status { get; set; }

    /// <summary>
    /// Release date (null for drafts)
    /// </summary>
    public DateTimeOffset? ReleaseDate { get; set; }

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
    /// Characters has no alignment
    /// </summary>
    public bool DisableAlignment { get; set; }

    /// <summary>
    /// Only GM and post author can see dice roll result
    /// </summary>
    public bool HideDiceResult { get; set; }

    /// <summary>
    /// Everyone can read each others private messages
    /// </summary>
    public bool ShowPrivateMessages { get; set; }

    /// <summary>
    /// Hide posts count and last post date for all users (except Master/Assistant)
    /// </summary>
    public bool HidePostStats { get; set; }

    /// <summary>
    /// Comments access mode
    /// </summary>
    public CommentsAccessMode CommentsAccessMode { get; set; }

    /// <summary>
    /// Attribute schema identifier
    /// </summary>
    public Guid? AttributeSchemaId { get; set; }

    /// <summary>
    /// Recruitment is open for new players
    /// </summary>
    public bool IsRecruitmentOpen { get; set; }

    /// <summary>
    /// Recruitment start timestamp (null for drafts)
    /// </summary>
    public DateTimeOffset? RecruitmentStartedUtc { get; set; }

    /// <summary>
    /// Game tag identifiers
    /// </summary>
    public IEnumerable<Guid> TagIds { get; set; } = [];

    /// <summary>
    /// Creation timestamp
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }
}

/// <summary>
/// Entity DTO for creating a room (repository level)
/// </summary>
public class CreateRoomEntity
{
    /// <summary>
    /// Room identifier
    /// </summary>
    public Guid RoomId { get; set; }

    /// <summary>
    /// Parent game identifier
    /// </summary>
    public Guid GameId { get; set; }

    /// <summary>
    /// Room title
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// Room type
    /// </summary>
    public RoomType Type { get; set; }

    /// <summary>
    /// Room access type
    /// </summary>
    public RoomAccessType AccessType { get; set; }

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

    /// <summary>
    /// Room order number
    /// </summary>
    public double OrderNumber { get; set; }
}

/// <summary>
/// Entity DTO for updating a game (repository level)
/// </summary>
public class UpdateGameEntity
{
    /// <summary>
    /// Game identifier
    /// </summary>
    public Guid GameId { get; set; }

    /// <summary>
    /// Game status (if changed)
    /// </summary>
    public ModuleStatus? Status { get; set; }

    /// <summary>
    /// Premoderation status (if changed)
    /// </summary>
    public PremoderationStatus? PremoderationStatus { get; set; }

    /// <summary>
    /// Game was completed successfully (if changed)
    /// </summary>
    public bool? IsFinished { get; set; }

    /// <summary>
    /// Game was frozen due to inactivity (if changed)
    /// </summary>
    public bool? IsFrozen { get; set; }

    /// <summary>
    /// Recruitment is open for new players (if changed)
    /// </summary>
    public bool? IsRecruitmentOpen { get; set; }

    /// <summary>
    /// Maximum number of players allowed (if changed)
    /// </summary>
    public int? RecruitmentPlayerLimit { get; set; }

    /// <summary>
    /// Game title (if changed)
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Game RPG system (if changed)
    /// </summary>
    public string? SystemName { get; set; }

    /// <summary>
    /// Narrative setting (if changed)
    /// </summary>
    public string? NarrativeSetting { get; set; }

    /// <summary>
    /// Game public information (if changed)
    /// </summary>
    public string? Info { get; set; }

    /// <summary>
    /// Hide character temper (if changed)
    /// </summary>
    public bool? HideTemper { get; set; }

    /// <summary>
    /// Hide character skills (if changed)
    /// </summary>
    public bool? HideSkills { get; set; }

    /// <summary>
    /// Hide character inventory (if changed)
    /// </summary>
    public bool? HideInventory { get; set; }

    /// <summary>
    /// Hide character story (if changed)
    /// </summary>
    public bool? HideStory { get; set; }

    /// <summary>
    /// Disable character alignment (if changed)
    /// </summary>
    public bool? DisableAlignment { get; set; }

    /// <summary>
    /// Hide dice roll result (if changed)
    /// </summary>
    public bool? HideDiceResult { get; set; }

    /// <summary>
    /// Show private messages (if changed)
    /// </summary>
    public bool? ShowPrivateMessages { get; set; }

    /// <summary>
    /// Hide post stats (if changed)
    /// </summary>
    public bool? HidePostStats { get; set; }

    /// <summary>
    /// Comments access mode (if changed)
    /// </summary>
    public CommentsAccessMode? CommentsAccessMode { get; set; }

    /// <summary>
    /// Game tag identifiers (if changed)
    /// </summary>
    public IEnumerable<Guid>? TagIds { get; set; }

    /// <summary>
    /// Update timestamp
    /// </summary>
    public DateTimeOffset UpdatedUtc { get; set; }
}

/// <summary>
/// Entity DTO for updating a room (repository level)
/// </summary>
public class UpdateRoomEntity
{
    /// <summary>
    /// Room identifier
    /// </summary>
    public Guid RoomId { get; set; }

    /// <summary>
    /// Room title (if changed)
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Room type (if changed)
    /// </summary>
    public RoomType? Type { get; set; }

    /// <summary>
    /// Room access type (if changed)
    /// </summary>
    public RoomAccessType? AccessType { get; set; }

    /// <summary>
    /// Previous room identifier for reordering (if changed)
    /// </summary>
    public Guid? NewPreviousRoomId { get; set; }

    /// <summary>
    /// Whether position should be changed
    /// </summary>
    public bool ShouldReorder { get; set; }

    /// <summary>
    /// Any user can read private messages within posts (if changed)
    /// </summary>
    public bool? ViewPrivateText { get; set; }

    /// <summary>
    /// Any user can see dice roll results (if changed)
    /// </summary>
    public bool? ViewDiceResults { get; set; }

    /// <summary>
    /// Dice rolling is enabled in this room (if changed)
    /// </summary>
    public bool? DiceEnabled { get; set; }

    /// <summary>
    /// Soft delete flag (if changed)
    /// </summary>
    public bool? IsRemoved { get; set; }
}

/// <summary>
/// Entity DTO for creating a post (repository level)
/// </summary>
public class CreatePostEntity
{
    /// <summary>
    /// Post identifier
    /// </summary>
    public Guid PostId { get; set; }

    /// <summary>
    /// Room identifier
    /// </summary>
    public Guid RoomId { get; set; }

    /// <summary>
    /// Author user identifier
    /// </summary>
    public Guid AuthorId { get; set; }

    /// <summary>
    /// Character identifier
    /// </summary>
    public Guid? CharacterId { get; set; }

    /// <summary>
    /// Post text
    /// </summary>
    public string Text { get; set; } = null!;

    /// <summary>
    /// Comment text
    /// </summary>
    public string? Comment { get; set; }

    /// <summary>
    /// Master message
    /// </summary>
    public string? MasterMessage { get; set; }

    /// <summary>
    /// Creation timestamp
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }
}

/// <summary>
/// Entity DTO for updating a post (repository level)
/// </summary>
public class UpdatePostEntity
{
    /// <summary>
    /// Post identifier
    /// </summary>
    public Guid PostId { get; set; }

    /// <summary>
    /// Character identifier (if changed)
    /// </summary>
    public Guid? CharacterId { get; set; }

    /// <summary>
    /// Whether character should be changed
    /// </summary>
    public bool ShouldChangeCharacter { get; set; }

    /// <summary>
    /// Post text
    /// </summary>
    public string Text { get; set; } = null!;

    /// <summary>
    /// Comment text
    /// </summary>
    public string? Comment { get; set; }

    /// <summary>
    /// Master message
    /// </summary>
    public string? MasterMessage { get; set; }

    /// <summary>
    /// Modified timestamp
    /// </summary>
    public DateTimeOffset ModifiedUtc { get; set; }

    /// <summary>
    /// Soft delete flag (if changed)
    /// </summary>
    public bool? IsRemoved { get; set; }
}

/// <summary>
/// Character attribute for create/update
/// </summary>
public class CharacterAttributeInput
{
    /// <summary>
    /// Attribute specification identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Attribute value
    /// </summary>
    public string Value { get; set; } = null!;
}

/// <summary>
/// Entity DTO for creating a character (repository level)
/// </summary>
public class CreateCharacterEntity
{
    /// <summary>
    /// Character identifier
    /// </summary>
    public Guid CharacterId { get; set; }

    /// <summary>
    /// Game identifier
    /// </summary>
    public Guid GameId { get; set; }

    /// <summary>
    /// Author user identifier (null for NPC)
    /// </summary>
    public Guid? AuthorId { get; set; }

    /// <summary>
    /// Character name
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// Character race
    /// </summary>
    public string? Race { get; set; }

    /// <summary>
    /// Character class
    /// </summary>
    public string? Class { get; set; }

    /// <summary>
    /// Character alignment
    /// </summary>
    public Alignment? Alignment { get; set; }

    /// <summary>
    /// Character appearance
    /// </summary>
    public string? Appearance { get; set; }

    /// <summary>
    /// Character temper
    /// </summary>
    public string? Temper { get; set; }

    /// <summary>
    /// Character story
    /// </summary>
    public string? Story { get; set; }

    /// <summary>
    /// Character skills
    /// </summary>
    public string? Skills { get; set; }

    /// <summary>
    /// Character inventory
    /// </summary>
    public string? Inventory { get; set; }

    /// <summary>
    /// Character is NPC
    /// </summary>
    public bool IsNpc { get; set; }

    /// <summary>
    /// Character access policy
    /// </summary>
    public CharacterAccessPolicy AccessPolicy { get; set; }

    /// <summary>
    /// Initial character status
    /// </summary>
    public CharacterStatus InitialStatus { get; set; }

    /// <summary>
    /// Character attributes
    /// </summary>
    public IEnumerable<CharacterAttributeInput> Attributes { get; set; } = [];

    /// <summary>
    /// Creation timestamp
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }
}

/// <summary>
/// Entity DTO for updating a character (repository level)
/// </summary>
public class UpdateCharacterEntity
{
    /// <summary>
    /// Character identifier
    /// </summary>
    public Guid CharacterId { get; set; }

    /// <summary>
    /// Character status (if changed)
    /// </summary>
    public CharacterStatus? Status { get; set; }

    /// <summary>
    /// Character died in game (if changed)
    /// </summary>
    public bool? IsDead { get; set; }

    /// <summary>
    /// Player left the game voluntarily (if changed)
    /// </summary>
    public bool? IsPlayerLeft { get; set; }

    /// <summary>
    /// Player was exiled from the game by GM (if changed)
    /// </summary>
    public bool? IsPlayerExiled { get; set; }

    /// <summary>
    /// Character name (if changed)
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Character race (if changed)
    /// </summary>
    public string? Race { get; set; }

    /// <summary>
    /// Character class (if changed)
    /// </summary>
    public string? Class { get; set; }

    /// <summary>
    /// Character alignment (if changed)
    /// </summary>
    public Alignment? Alignment { get; set; }

    /// <summary>
    /// Character appearance (if changed)
    /// </summary>
    public string? Appearance { get; set; }

    /// <summary>
    /// Character temper (if changed)
    /// </summary>
    public string? Temper { get; set; }

    /// <summary>
    /// Character story (if changed)
    /// </summary>
    public string? Story { get; set; }

    /// <summary>
    /// Character skills (if changed)
    /// </summary>
    public string? Skills { get; set; }

    /// <summary>
    /// Character inventory (if changed)
    /// </summary>
    public string? Inventory { get; set; }

    /// <summary>
    /// Character is NPC (if changed)
    /// </summary>
    public bool? IsNpc { get; set; }

    /// <summary>
    /// Character access policy (if changed)
    /// </summary>
    public CharacterAccessPolicy? AccessPolicy { get; set; }

    /// <summary>
    /// Character attributes (if changed)
    /// </summary>
    public IEnumerable<CharacterAttributeInput>? Attributes { get; set; }

    /// <summary>
    /// Modified timestamp
    /// </summary>
    public DateTimeOffset ModifiedUtc { get; set; }
}

/// <summary>
/// Entity DTO for creating a game comment (repository level)
/// </summary>
public class CreateGameCommentEntity
{
    /// <summary>
    /// Comment identifier
    /// </summary>
    public Guid CommentId { get; set; }

    /// <summary>
    /// Game identifier
    /// </summary>
    public Guid GameId { get; set; }

    /// <summary>
    /// Author user identifier
    /// </summary>
    public Guid AuthorId { get; set; }

    /// <summary>
    /// Comment text
    /// </summary>
    public string Text { get; set; } = null!;

    /// <summary>
    /// New comment count (after creating this comment)
    /// </summary>
    public int NewCommentCount { get; set; }

    /// <summary>
    /// Creation timestamp
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }
}

/// <summary>
/// Entity DTO for updating a game comment (repository level)
/// </summary>
public class UpdateGameCommentEntity
{
    /// <summary>
    /// Comment identifier
    /// </summary>
    public Guid CommentId { get; set; }

    /// <summary>
    /// Updated comment text
    /// </summary>
    public string Text { get; set; } = null!;

    /// <summary>
    /// Last edit timestamp
    /// </summary>
    public DateTimeOffset ModifiedUtc { get; set; }
}

/// <summary>
/// Entity DTO for deleting a game comment (repository level)
/// </summary>
public class DeleteGameCommentEntity
{
    /// <summary>
    /// Comment identifier
    /// </summary>
    public Guid CommentId { get; set; }

    /// <summary>
    /// Game identifier
    /// </summary>
    public Guid GameId { get; set; }

    /// <summary>
    /// New comment count after deletion
    /// </summary>
    public int NewCommentCount { get; set; }

    /// <summary>
    /// New last comment ID after deletion
    /// </summary>
    public Guid? NewLastCommentId { get; set; }
}

/// <summary>
/// Entity DTO for creating a game invitation (repository level)
/// </summary>
public class CreateGameInvitationEntity
{
    /// <summary>
    /// Token identifier (pre-generated)
    /// </summary>
    public Guid TokenId { get; set; }

    /// <summary>
    /// Game identifier
    /// </summary>
    public Guid GameId { get; set; }

    /// <summary>
    /// User identifier (who is being invited)
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// User who creates the invitation
    /// </summary>
    public Guid CreatorId { get; set; }

    /// <summary>
    /// Token type (GameAssistantInvitation, GamePlayerInvitation, GameReaderInvitation)
    /// </summary>
    public TokenType TokenType { get; set; }

    /// <summary>
    /// Creation timestamp
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }
}

/// <summary>
/// Entity DTO for adding an assistant (repository level)
/// </summary>
public class AddAssistantEntity
{
    /// <summary>
    /// GameAssistant link identifier (pre-generated)
    /// </summary>
    public Guid GameAssistantId { get; set; }

    /// <summary>
    /// Game identifier
    /// </summary>
    public Guid GameId { get; set; }

    /// <summary>
    /// User identifier
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Joined timestamp
    /// </summary>
    public DateTimeOffset JoinedUtc { get; set; }
}

/// <summary>
/// Entity DTO for updating master (repository level)
/// </summary>
public class UpdateMasterEntity
{
    /// <summary>
    /// Game identifier
    /// </summary>
    public Guid GameId { get; set; }

    /// <summary>
    /// New master user identifier
    /// </summary>
    public Guid NewMasterId { get; set; }

    /// <summary>
    /// Old master user identifier (becomes assistant)
    /// </summary>
    public Guid OldMasterId { get; set; }

    /// <summary>
    /// GameAssistant link identifier for old master (pre-generated)
    /// </summary>
    public Guid NewAssistantId { get; set; }

    /// <summary>
    /// When the old master became assistant
    /// </summary>
    public DateTimeOffset AssistantJoinedUtc { get; set; }
}

/// <summary>
/// Entity DTO for creating room access (repository level)
/// </summary>
public class CreateRoomAccessEntity
{
    /// <summary>
    /// Access identifier
    /// </summary>
    public Guid AccessId { get; set; }

    /// <summary>
    /// Room identifier
    /// </summary>
    public Guid RoomId { get; set; }

    /// <summary>
    /// Character identifier (for character access)
    /// </summary>
    public Guid? CharacterId { get; set; }

    /// <summary>
    /// Reader user identifier (for reader access)
    /// </summary>
    public Guid? ReaderUserId { get; set; }

    /// <summary>
    /// Access policy
    /// </summary>
    public RoomAccessPolicy Policy { get; set; }
}

/// <summary>
/// Entity DTO for updating room access (repository level)
/// </summary>
public class UpdateRoomAccessEntity
{
    /// <summary>
    /// Access identifier
    /// </summary>
    public Guid AccessId { get; set; }

    /// <summary>
    /// Access policy
    /// </summary>
    public RoomAccessPolicy Policy { get; set; }
}

/// <summary>
/// Entity DTO for creating post pendency (repository level)
/// </summary>
public class CreatePostPendencyEntity
{
    /// <summary>
    /// Pendency identifier
    /// </summary>
    public Guid PendencyId { get; set; }

    /// <summary>
    /// Room identifier
    /// </summary>
    public Guid RoomId { get; set; }

    /// <summary>
    /// Character identifier
    /// </summary>
    public Guid CharacterId { get; set; }

    /// <summary>
    /// User waiting for post
    /// </summary>
    public Guid WaitingForUserId { get; set; }

    /// <summary>
    /// Creator user identifier
    /// </summary>
    public Guid CreatedById { get; set; }

    /// <summary>
    /// Creation timestamp
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }
}

/// <summary>
/// Entity DTO for creating a game review (repository level)
/// </summary>
public class CreateGameReviewEntity
{
    /// <summary>
    /// Review identifier
    /// </summary>
    public Guid ReviewId { get; set; }

    /// <summary>
    /// Author user identifier
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Target game identifier
    /// </summary>
    public Guid GameId { get; set; }

    /// <summary>
    /// Creation timestamp
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Review text
    /// </summary>
    public required string Text { get; set; }
}

/// <summary>
/// Entity DTO for updating a game review (repository level)
/// </summary>
/// <param name="ReviewId">Review identifier</param>
/// <param name="Text">Updated text (null to keep current)</param>
/// <param name="IsRemoved">Updated removed status (null to keep current)</param>
/// <param name="ModifiedUtc">Modification timestamp</param>
/// <param name="ModifiedByUserId">User who modified the review</param>
public record UpdateGameReviewEntity(
    Guid ReviewId,
    string? Text = null,
    bool? IsRemoved = null,
    DateTimeOffset? ModifiedUtc = null,
    Guid? ModifiedByUserId = null);

/// <summary>
/// Filter parameters for game reviews
/// </summary>
public class GameReviewFilter
{
    /// <summary>
    /// Filter by review author ID
    /// </summary>
    public Guid? AuthorId { get; set; }

    /// <summary>
    /// Filter by game ID
    /// </summary>
    public Guid? GameId { get; set; }

    /// <summary>
    /// Filter by games where user is GM
    /// </summary>
    public Guid? GmId { get; set; }
}

/// <summary>
/// Entity DTO for creating a post review (repository level)
/// </summary>
public class CreatePostReviewEntity
{
    /// <summary>
    /// Review identifier
    /// </summary>
    public Guid ReviewId { get; set; }

    /// <summary>
    /// Author user identifier
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Target post identifier
    /// </summary>
    public Guid PostId { get; set; }

    /// <summary>
    /// Post author identifier (for denormalization)
    /// </summary>
    public Guid PostAuthorId { get; set; }

    /// <summary>
    /// Game identifier (for cooldown check)
    /// </summary>
    public Guid GameId { get; set; }

    /// <summary>
    /// Creation timestamp
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Review sentiment sign
    /// </summary>
    public ReviewSign Sign { get; set; }

    /// <summary>
    /// Optional reason type
    /// </summary>
    public ReviewReasonType? ReasonType { get; set; }
}

/// <summary>
/// Entity DTO for updating a post review (repository level)
/// </summary>
/// <param name="ReviewId">Review identifier</param>
/// <param name="Sign">Updated sign (null to keep current)</param>
/// <param name="ReasonType">Updated reason type (null to keep current)</param>
/// <param name="IsRemoved">Updated removed status (null to keep current)</param>
/// <param name="ModifiedUtc">Modification timestamp</param>
/// <param name="ModifiedByUserId">User who modified the review</param>
public record UpdatePostReviewEntity(
    Guid ReviewId,
    ReviewSign? Sign = null,
    ReviewReasonType? ReasonType = null,
    bool? IsRemoved = null,
    DateTimeOffset? ModifiedUtc = null,
    Guid? ModifiedByUserId = null);

/// <summary>
/// Filter parameters for post reviews queries
/// </summary>
public class PostReviewFilter
{
    /// <summary>
    /// Filter by review author ID (reviews BY this user)
    /// </summary>
    public Guid? AuthorId { get; set; }

    /// <summary>
    /// Filter by post author ID (reviews ON this user's posts)
    /// </summary>
    public Guid? RecipientId { get; set; }

    /// <summary>
    /// Filter by game ID (reviews on posts in this game)
    /// </summary>
    public Guid? GameId { get; set; }
}

/// <summary>
/// Post information for review creation
/// </summary>
public class PostInfo
{
    /// <summary>
    /// Post author identifier
    /// </summary>
    public Guid AuthorId { get; set; }

    /// <summary>
    /// Game identifier
    /// </summary>
    public Guid GameId { get; set; }
}

/// <summary>
/// DTO for creating an attribute schema
/// </summary>
public class CreateAttributeSchema
{
    /// <summary>
    /// Schema title
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// Schema access type
    /// </summary>
    public SchemaType Type { get; set; }

    /// <summary>
    /// Attribute specifications
    /// </summary>
    public IEnumerable<CreateAttributeSpecification> Specifications { get; set; } = [];
}

/// <summary>
/// DTO for creating an attribute specification
/// </summary>
public class CreateAttributeSpecification
{
    /// <summary>
    /// Specification title
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// Specification constraint (optional JSON)
    /// </summary>
    public string? Constraint { get; set; }

    /// <summary>
    /// Order index
    /// </summary>
    public int Order { get; set; }
}

/// <summary>
/// DTO for updating an attribute schema
/// </summary>
public class UpdateAttributeSchema
{
    /// <summary>
    /// Schema identifier
    /// </summary>
    public Guid SchemaId { get; set; }

    /// <summary>
    /// Schema title
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Schema access type
    /// </summary>
    public SchemaType? Type { get; set; }

    /// <summary>
    /// Attribute specifications (replace all)
    /// </summary>
    public IEnumerable<UpdateAttributeSpecification>? Specifications { get; set; }
}

/// <summary>
/// DTO for updating an attribute specification
/// </summary>
public class UpdateAttributeSpecification
{
    /// <summary>
    /// Specification identifier (null for new)
    /// </summary>
    public Guid? Id { get; set; }

    /// <summary>
    /// Specification title
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// Specification constraint (optional JSON)
    /// </summary>
    public string? Constraint { get; set; }

    /// <summary>
    /// Order index
    /// </summary>
    public int Order { get; set; }
}
