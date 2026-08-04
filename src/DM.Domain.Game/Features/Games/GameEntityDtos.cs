using System;
using System.Collections.Generic;
using DM.Domain.Core.Content;
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
    /// Game master user identifier
    /// </summary>
    public Guid MasterId { get; set; }

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
    /// Visibility of draft content (when Status = Draft)
    /// </summary>
    public DraftVisibility DraftVisibility { get; set; }

    /// <summary>
    /// Activation date (null for drafts, UTC)
    /// </summary>
    public DateTimeOffset? ActivatedUtc { get; set; }

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
    /// Number of times recruitment has been opened (1 for first activation)
    /// </summary>
    public int RecruitmentCount { get; set; }

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
    /// Room is archived (hidden from the active rooms list, kept for history)
    /// </summary>
    public bool IsArchived { get; set; }

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
    /// Reason why the game was closed (if changed)
    /// </summary>
    public ClosedReason? ClosedReason { get; set; }

    /// <summary>
    /// Visibility of draft content (if changed)
    /// </summary>
    public DraftVisibility? DraftVisibility { get; set; }

    /// <summary>
    /// Recruitment is open for new players (if changed)
    /// </summary>
    public bool? IsRecruitmentOpen { get; set; }

    /// <summary>
    /// Increment recruitment count (when reopening recruitment)
    /// </summary>
    public bool IncrementRecruitmentCount { get; set; }

    /// <summary>
    /// Maximum number of player characters allowed (if changed)
    /// </summary>
    public int? RecruitmentPcLimit { get; set; }

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

    /// <summary>
    /// Activation timestamp (set on first activation)
    /// </summary>
    public DateTimeOffset? ActivatedUtc { get; set; }

    /// <summary>
    /// Closed timestamp (set when closing)
    /// </summary>
    public DateTimeOffset? ClosedUtc { get; set; }

    /// <summary>
    /// Whether to clear ClosedUtc (when reopening)
    /// </summary>
    public bool ClearClosedUtc { get; set; }

    /// <summary>
    /// Curating mentor user id (only applied when <see cref="SetMentorId"/> is true;
    /// null clears the curator). Kept separate from the nullable value so that the
    /// repository can distinguish "leave the current mentor untouched" from
    /// "explicitly set the mentor to null".
    /// </summary>
    public Guid? MentorId { get; set; }

    /// <summary>
    /// Whether <see cref="MentorId"/> should be written (set or cleared).
    /// </summary>
    public bool SetMentorId { get; set; }

    /// <summary>
    /// Whether to clear RecruitmentStartedUtc (admin recruitment-date reset)
    /// </summary>
    public bool ClearRecruitmentStartedUtc { get; set; }
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
    /// Room is kept out of the room list for those without access (if changed)
    /// </summary>
    public bool? HiddenWithoutAccess { get; set; }

    /// <summary>
    /// Room is archived (if changed)
    /// </summary>
    public bool? IsArchived { get; set; }

    /// <summary>
    /// Linked chat identifier (for RoomType.Chat rooms)
    /// </summary>
    public Guid? ChatId { get; set; }

    /// <summary>
    /// Whether ChatId should be set
    /// </summary>
    public bool ShouldSetChatId { get; set; }

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
    /// Game text (in-character content)
    /// </summary>
    public string GameText { get; set; } = null!;

    /// <summary>
    /// Metagame text (OOC commentary)
    /// </summary>
    public string? MetagameText { get; set; }

    /// <summary>
    /// Owner user ids allowed to see each [private] block of
    /// <see cref="GameText"/>, resolved at save time and frozen.
    /// See <see cref="PrivateAddresseeSnapshot"/>.
    /// </summary>
    public string PrivateAddresseeSnapshotJson { get; set; } = PrivateAddresseeSnapshot.Empty;

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
    /// Game text (in-character content)
    /// </summary>
    public string GameText { get; set; } = null!;

    /// <summary>
    /// Metagame text (OOC commentary)
    /// </summary>
    public string? MetagameText { get; set; }

    /// <summary>
    /// Owner user ids allowed to see each [private] block of
    /// <see cref="GameText"/>. Recomputed on every save, keeping what an
    /// earlier one already resolved. See <see cref="PrivateAddresseeSnapshot"/>.
    /// </summary>
    public string PrivateAddresseeSnapshotJson { get; set; } = PrivateAddresseeSnapshot.Empty;

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

    /// <summary>
    /// User who removed the comment
    /// </summary>
    public Guid DeletedByUserId { get; set; }

    /// <summary>
    /// When the comment was removed
    /// </summary>
    public DateTimeOffset DeletedUtc { get; set; }
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
/// Entity DTO for creating a post review (repository level)
/// </summary>
public class CreatePostReviewEntity
{
    /// <summary>
    /// Review identifier
    /// </summary>
    public Guid PostReviewId { get; set; }

    /// <summary>
    /// Author user identifier
    /// </summary>
    public Guid AuthorId { get; set; }

    /// <summary>
    /// Target post identifier
    /// </summary>
    public Guid PostId { get; set; }

    /// <summary>
    /// Post author identifier (denormalized for efficient filtering)
    /// </summary>
    public Guid PostAuthorId { get; set; }

    /// <summary>
    /// Game identifier (denormalized for efficient filtering)
    /// </summary>
    public Guid GameId { get; set; }

    /// <summary>
    /// Creation timestamp (UTC)
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Review sentiment sign (+1, 0, -1)
    /// </summary>
    public ReviewSign Sign { get; set; }

    /// <summary>
    /// Review text (BBCode supported)
    /// </summary>
    public string Text { get; set; } = null!;
}

/// <summary>
/// Entity DTO for updating a post review (repository level)
/// </summary>
/// <param name="PostReviewId">Review identifier</param>
/// <param name="Sign">Updated sign (null to keep current)</param>
/// <param name="IsRemoved">Updated removed status (null to keep current)</param>
/// <param name="ModifiedUtc">Modification timestamp</param>
/// <param name="ModifiedByUserId">User who modified the review</param>
public record UpdatePostReviewEntity(
    Guid PostReviewId,
    ReviewSign? Sign = null,
    bool? IsRemoved = null,
    DateTimeOffset? ModifiedUtc = null,
    Guid? ModifiedByUserId = null);

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
/// Shared shape of an attribute specification on the write path
/// </summary>
public interface IAttributeSpecificationInput
{
    /// <summary>
    /// Specification title
    /// </summary>
    string Title { get; }

    /// <summary>
    /// Attribute type
    /// </summary>
    AttributeSpecificationType Type { get; }

    /// <summary>
    /// Maximum length (text/number/BBCode types)
    /// </summary>
    int? MaxLength { get; }

    /// <summary>
    /// Available values (list types)
    /// </summary>
    IEnumerable<ListValue> Values { get; }

    /// <summary>
    /// Is descriptor attribute
    /// </summary>
    bool IsDescriptor { get; }
}

/// <summary>
/// DTO for creating an attribute specification
/// </summary>
public class CreateAttributeSpecification : IAttributeSpecificationInput
{
    /// <summary>
    /// Specification title
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// Attribute type
    /// </summary>
    public AttributeSpecificationType Type { get; set; }

    /// <summary>
    /// Value is required
    /// </summary>
    public bool Required { get; set; }

    /// <summary>
    /// Is descriptor attribute (shown on game main page)
    /// </summary>
    public bool IsDescriptor { get; set; }

    /// <summary>
    /// Is hidden attribute (only GM and owner can see)
    /// </summary>
    public bool IsHidden { get; set; }

    /// <summary>
    /// Maximum length (text/number/BBCode types)
    /// </summary>
    public int? MaxLength { get; set; }

    /// <summary>
    /// Available values (list types)
    /// </summary>
    public IEnumerable<ListValue> Values { get; set; } = [];

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
public class UpdateAttributeSpecification : IAttributeSpecificationInput
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
    /// Attribute type
    /// </summary>
    public AttributeSpecificationType Type { get; set; }

    /// <summary>
    /// Value is required
    /// </summary>
    public bool Required { get; set; }

    /// <summary>
    /// Is descriptor attribute (shown on game main page)
    /// </summary>
    public bool IsDescriptor { get; set; }

    /// <summary>
    /// Is hidden attribute (only GM and owner can see)
    /// </summary>
    public bool IsHidden { get; set; }

    /// <summary>
    /// Maximum length (text/number/BBCode types)
    /// </summary>
    public int? MaxLength { get; set; }

    /// <summary>
    /// Available values (list types)
    /// </summary>
    public IEnumerable<ListValue> Values { get; set; } = [];

    /// <summary>
    /// Order index
    /// </summary>
    public int Order { get; set; }
}
