using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Infrastructure.Persistence.Entities.Contracts;
using DM.Infrastructure.Persistence.Entities.Account;
using DM.Infrastructure.Persistence.Entities.Game.Posts;
using DM.Domain.Core.Enums;

namespace DM.Infrastructure.Persistence.Entities.Game;

/// <summary>
/// DAL model for post review (review of a game post with rating)
/// </summary>
/// <remarks>
/// BBCode is supported in the Text field.
/// Any sentiment text is allowed.
/// Has Sign (+1/0/-1) for rating impact.
/// One review per author-post pair.
/// SUPPORTS LIKES (only entity with likes in the review system).
/// </remarks>
[Table("PostReviews")]
public class PostReview : ISoftDeletable
{
    /// <summary>
    /// Review identifier
    /// </summary>
    [Key]
    public Guid PostReviewId { get; set; }

    /// <summary>
    /// Author identifier (user writing the review)
    /// </summary>
    public Guid AuthorId { get; set; }

    /// <summary>
    /// Post identifier (post being reviewed)
    /// </summary>
    public Guid PostId { get; set; }

    /// <summary>
    /// Post author identifier for efficient filtering (denormalized)
    /// </summary>
    public Guid PostAuthorId { get; set; }

    /// <summary>
    /// Game identifier for efficient filtering (denormalized)
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
    /// Review text (BBCode supported, optional)
    /// </summary>
    public string? Text { get; set; }

    /// <summary>
    /// Rating impact sign value for storage (+1, 0, -1)
    /// </summary>
    public short SignValue { get; set; }

    /// <summary>
    /// Rating impact sign
    /// </summary>
    [NotMapped]
    public ReviewSign Sign
    {
        get => (ReviewSign)SignValue;
        set => SignValue = (short)value;
    }

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
    /// Post being reviewed
    /// </summary>
    [ForeignKey(nameof(PostId))]
    public virtual Post Post { get; set; } = null!;

    /// <summary>
    /// Post author (denormalized for efficient queries)
    /// </summary>
    [ForeignKey(nameof(PostAuthorId))]
    public virtual User PostAuthor { get; set; } = null!;

    /// <summary>
    /// Game (denormalized for efficient queries)
    /// </summary>
    [ForeignKey(nameof(GameId))]
    public virtual Game Game { get; set; } = null!;

    /// <summary>
    /// Last editor
    /// </summary>
    [ForeignKey(nameof(ModifiedByUserId))]
    public virtual User? ModifiedBy { get; set; }
}
