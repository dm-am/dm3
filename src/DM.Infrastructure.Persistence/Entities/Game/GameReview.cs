using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Infrastructure.Persistence.Entities.Contracts;
using DM.Infrastructure.Persistence.Entities.Account;

namespace DM.Infrastructure.Persistence.Entities.Game;

/// <summary>
/// DAL model for game review (review of a game by a player)
/// </summary>
/// <remarks>
/// BBCode is supported in the Text field.
/// Any sentiment text is allowed (positive, negative, neutral).
/// One review per author-game pair.
/// No likes support.
/// </remarks>
[Table("GameReviews")]
public class GameReview : ISoftDeletable
{
    /// <summary>
    /// Review identifier
    /// </summary>
    [Key]
    public Guid GameReviewId { get; set; }

    /// <summary>
    /// Author identifier (user writing the review)
    /// </summary>
    public Guid AuthorId { get; set; }

    /// <summary>
    /// Game identifier (game being reviewed)
    /// </summary>
    public Guid GameId { get; set; }

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
    /// Review text (BBCode supported)
    /// </summary>
    [Required]
    public string Text { get; set; } = string.Empty;

    /// <inheritdoc />
    public bool IsRemoved { get; set; }

    /// <inheritdoc />
    public Guid? DeletedByUserId { get; set; }

    /// <inheritdoc />
    public DateTimeOffset? DeletedUtc { get; set; }

    /// <summary>
    /// Author (user writing the review)
    /// </summary>
    [ForeignKey(nameof(AuthorId))]
    public virtual User Author { get; set; } = null!;

    /// <summary>
    /// User who deleted the review
    /// </summary>
    [ForeignKey(nameof(DeletedByUserId))]
    public virtual User? DeletedBy { get; set; }

    /// <summary>
    /// Game being reviewed
    /// </summary>
    [ForeignKey(nameof(GameId))]
    public virtual Game Game { get; set; } = null!;

    /// <summary>
    /// Last editor
    /// </summary>
    [ForeignKey(nameof(ModifiedByUserId))]
    public virtual User? ModifiedBy { get; set; }
}
