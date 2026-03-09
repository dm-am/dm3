using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Infrastructure.Persistence.Entities.DataContracts;
using DM.Infrastructure.Persistence.Entities.Game;
using DM.Infrastructure.Persistence.Entities.Account;
using DM.Domain.Core.Enums;

namespace DM.Infrastructure.Persistence.Entities.CrossDomain;

/// <summary>
/// DAL model for review (platform, user, game, post reviews)
/// </summary>
/// <remarks>
/// TargetId is a polymorphic reference:
/// - For Platform reviews: null
/// - For User reviews: UserId of target user
/// - For Game reviews: GameId of target game
/// - For Post reviews: PostId of target post
/// Target entities are loaded manually based on TargetType.
/// </remarks>
[Table("Reviews")]
public class Review : IRemovable
{
    /// <summary>
    /// Review identifier
    /// </summary>
    [Key]
    public Guid ReviewId { get; set; }

    /// <summary>
    /// Author identifier
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Type of entity being reviewed
    /// </summary>
    public ReviewTargetType TargetType { get; set; } = ReviewTargetType.Platform;

    /// <summary>
    /// Target entity identifier (UserId for User reviews, GameId for Game reviews, PostId for Post reviews, null for Platform)
    /// </summary>
    public Guid? TargetId { get; set; }

    /// <summary>
    /// Creation moment (UTC)
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Last modification moment (UTC)
    /// </summary>
    public DateTimeOffset? ModifiedUtc { get; set; }

    /// <summary>
    /// Last editor user identifier
    /// </summary>
    public Guid? ModifiedByUserId { get; set; }

    /// <summary>
    /// Review content (optional for Post reviews)
    /// </summary>
    public string? Text { get; set; }

    /// <summary>
    /// Premoderation flag (only applicable for Platform reviews)
    /// </summary>
    public bool IsApproved { get; set; }

    /// <inheritdoc />
    public bool IsRemoved { get; set; }

    #region Post Review Fields (only used when TargetType = Post)

    /// <summary>
    /// Rating impact sign value for storage (only for Post reviews)
    /// </summary>
    public short? SignValue { get; set; }

    /// <summary>
    /// Rating impact sign (only for Post reviews)
    /// </summary>
    [NotMapped]
    public ReviewSign? Sign => SignValue.HasValue ? (ReviewSign)SignValue.Value : null;

    /// <summary>
    /// Review reason type (only for Post reviews)
    /// </summary>
    public ReviewReasonType? ReasonType { get; set; }

    /// <summary>
    /// Post author identifier for efficient filtering (denormalized, only for Post reviews)
    /// </summary>
    public Guid? PostAuthorId { get; set; }

    /// <summary>
    /// Game identifier for efficient filtering (denormalized, only for Post reviews)
    /// </summary>
    public Guid? GameId { get; set; }

    #endregion

    /// <summary>
    /// Author
    /// </summary>
    [ForeignKey(nameof(UserId))]
    public virtual User Author { get; set; } = null!;

    /// <summary>
    /// Last editor
    /// </summary>
    [ForeignKey(nameof(ModifiedByUserId))]
    public virtual User? ModifiedBy { get; set; }

    /// <summary>
    /// Post author (only for Post reviews)
    /// </summary>
    [ForeignKey(nameof(PostAuthorId))]
    public virtual User? PostAuthor { get; set; }

    /// <summary>
    /// Game (only for Post reviews)
    /// </summary>
    [ForeignKey(nameof(GameId))]
    public virtual Game.Game? Game { get; set; }
}
