using DM.Domain.Core.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Infrastructure.Persistence.Entities.CrossDomain;
using DM.Infrastructure.Persistence.Entities.DataContracts;
using DM.Infrastructure.Persistence.Entities.Game.Characters;
using DM.Infrastructure.Persistence.Entities.Game.Links;
using DM.Infrastructure.Persistence.Entities.Game.Posts;
using DM.Infrastructure.Persistence.Entities.Account;

namespace DM.Infrastructure.Persistence.Entities.Game;

/// <summary>
/// DAL model for game
/// </summary>
[Table("Games")]
public class Game : IRemovable
{
    /// <summary>
    /// Game identifier
    /// </summary>
    [Key]
    public Guid GameId { get; set; }

    /// <summary>
    /// Creation moment (UTC)
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Release moment (first time the game started requirement)
    /// </summary>
    public DateTimeOffset? ReleaseDate { get; set; }

    /// <summary>
    /// Status
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
    /// Recruitment is open for new players
    /// </summary>
    public bool IsRecruitmentOpen { get; set; }

    /// <summary>
    /// Maximum number of players allowed (null = unlimited)
    /// </summary>
    public int? RecruitmentPlayerLimit { get; set; }

    /// <summary>
    /// When the recruitment was opened
    /// </summary>
    public DateTimeOffset? RecruitmentStartedUtc { get; set; }

    /// <summary>
    /// When the game was closed
    /// </summary>
    public DateTimeOffset? ClosedUtc { get; set; }

    /// <summary>
    /// Author (GM) identifier
    /// </summary>
    public Guid AuthorId { get; set; }

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
    /// Narrative setting (e.g. Mass Effect, WarHammer, Our world)
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

    /// <summary>
    /// Game author (Master/GM)
    /// </summary>
    [ForeignKey(nameof(AuthorId))]
    public User Author { get; set; } = null!;

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

    /// <summary>
    /// Game preview picture
    /// </summary>
    [InverseProperty(nameof(Upload.Game))]
    public virtual ICollection<Upload> Pictures { get; set; } = [];

    /// <summary>
    /// Game authorization tokens
    /// </summary>
    [InverseProperty(nameof(Token.Game))]
    public virtual ICollection<Token> Tokens { get; set; } = [];
}