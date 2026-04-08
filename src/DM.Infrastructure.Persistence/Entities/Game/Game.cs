using DM.Domain.Core.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Infrastructure.Persistence.Entities.Shared;
using DM.Infrastructure.Persistence.Entities.Contracts;
using DM.Infrastructure.Persistence.Entities.Game.Characters;
using DM.Infrastructure.Persistence.Entities.Game.Links;
using DM.Infrastructure.Persistence.Entities.Game.Posts;
using DM.Infrastructure.Persistence.Entities.Account;

namespace DM.Infrastructure.Persistence.Entities.Game;

/// <summary>
/// DAL model for game
/// </summary>
[Table("Games")]
public class Game : ISoftDeletable
{
    /// <summary>
    /// Game identifier
    /// </summary>
    [Key]
    public Guid GameId { get; set; }

    /// <summary>
    /// Auto-incrementing serial number for PublicId generation
    /// </summary>
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int SerialNumber { get; set; }

    /// <summary>
    /// Short public identifier for URLs (5 lowercase letters)
    /// </summary>
    public string PublicId { get; set; } = null!;

    /// <summary>
    /// Creation moment (UTC)
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Activation moment (first time the game became active, UTC)
    /// </summary>
    public DateTimeOffset? ActivatedUtc { get; set; }

    /// <summary>
    /// Status
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
    /// Recruitment is open for new players
    /// </summary>
    public bool IsRecruitmentOpen { get; set; }

    /// <summary>
    /// Maximum number of player characters allowed (null = unlimited)
    /// </summary>
    public int? RecruitmentPcLimit { get; set; }

    /// <summary>
    /// When the recruitment was opened
    /// </summary>
    public DateTimeOffset? RecruitmentStartedUtc { get; set; }

    /// <summary>
    /// Number of times recruitment has been opened (0 = never, 1 = first, 2+ = subsequent/донабор)
    /// </summary>
    public int RecruitmentCount { get; set; }

    /// <summary>
    /// When the game was closed
    /// </summary>
    public DateTimeOffset? ClosedUtc { get; set; }

    /// <summary>
    /// When the last post was created (denormalized for inactivity tracking)
    /// </summary>
    public DateTimeOffset? LastPostCreatedUtc { get; set; }

    /// <summary>
    /// When the inactivity warning was sent (null = no warning yet)
    /// </summary>
    public DateTimeOffset? InactivityWarningUtc { get; set; }

    /// <summary>
    /// When the closure warning was sent for frozen games (null = no warning yet)
    /// </summary>
    public DateTimeOffset? ClosureWarningUtc { get; set; }

    /// <summary>
    /// Pre-computed popularity score for efficient sorting.
    /// Score = active players (unique authors of active characters) + active readers (subscribers active within 30 days).
    /// Updated by PopularityScoreService background job.
    /// </summary>
    public int PopularityScore { get; set; }

    /// <summary>
    /// When the popularity score was last recalculated (UTC)
    /// </summary>
    public DateTimeOffset? PopularityScoreUpdatedUtc { get; set; }

    /// <summary>
    /// Game master (GM) identifier
    /// </summary>
    public Guid MasterId { get; set; }

    /// <summary>
    /// Premoderation assistant identifier
    /// </summary>
    public Guid? MentorId { get; set; }

    /// <summary>
    /// Character attribute schema identifier
    /// </summary>
    public Guid? AttributeSchemaId { get; set; }

    /// <summary>
    /// Title
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// System name (e.g. D&amp;D, WoD)
    /// </summary>
    public string? SystemName { get; set; }

    /// <summary>
    /// Narrative setting (e.g. Mass Effect, Warhammer, Our world)
    /// </summary>
    public string? NarrativeSetting { get; set; }

    /// <summary>
    /// Full game information
    /// </summary>
    public string? Info { get; set; }

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
    /// Any user can read private messages within posts
    /// </summary>
    public bool ShowPrivateMessages { get; set; }

    /// <summary>
    /// Hide posts count and last post date for all participants (except Master/Assistant)
    /// </summary>
    public bool HidePostStats { get; set; }

    /// <summary>
    /// Policy for game comments read/write access
    /// </summary>
    public CommentsAccessMode CommentsAccessMode { get; set; }

    /// <summary>
    /// Total comments count (denormalized)
    /// </summary>
    public int CommentCount { get; set; }

    /// <summary>
    /// Last comment identifier (denormalized for navigation)
    /// </summary>
    public Guid? LastCommentId { get; set; }

    /// <inheritdoc />
    public bool IsRemoved { get; set; }

    /// <inheritdoc />
    public Guid? DeletedByUserId { get; set; }

    /// <inheritdoc />
    public DateTimeOffset? DeletedUtc { get; set; }

    /// <summary>
    /// Game master (GM)
    /// </summary>
    [ForeignKey(nameof(MasterId))]
    public User Master { get; set; } = null!;

    /// <summary>
    /// User who deleted the game
    /// </summary>
    [ForeignKey(nameof(DeletedByUserId))]
    public User? DeletedBy { get; set; }

    /// <summary>
    /// Premoderation assistant
    /// </summary>
    [ForeignKey(nameof(MentorId))]
    public User? Mentor { get; set; }

    /// <summary>
    /// GM assistants
    /// </summary>
    [InverseProperty(nameof(GameAssistant.Game))]
    public virtual ICollection<GameAssistant> Assistants { get; set; } = [];

    /// <summary>
    /// Blacklist links
    /// </summary>
    [InverseProperty(nameof(GameBlacklist.Game))]
    public virtual ICollection<GameBlacklist> BlackList { get; set; } = [];

    /// <summary>
    /// Game tags
    /// </summary>
    [InverseProperty(nameof(GameTag.Game))]
    public virtual ICollection<GameTag> GameTags { get; set; } = [];

    /// <summary>
    /// Characters
    /// </summary>
    [InverseProperty(nameof(Character.Game))]
    public virtual ICollection<Character> Characters { get; set; } = [];

    /// <summary>
    /// Rooms
    /// </summary>
    [InverseProperty(nameof(Room.Game))]
    public virtual ICollection<Room> Rooms { get; set; } = [];

    /// <summary>
    /// comments (polymorphic - loaded manually via EntityId)
    /// </summary>
    [NotMapped]
    public virtual ICollection<Comment> Comments { get; set; } = [];

    // NOTE: Pictures navigation removed - Upload.EntityId is polymorphic without FK constraints

    /// <summary>
    /// Game authorization tokens
    /// </summary>
    [InverseProperty(nameof(Token.Game))]
    public virtual ICollection<Token> Tokens { get; set; } = [];
}