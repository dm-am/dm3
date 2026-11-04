using System;
using DM.Domain.Core.Enums;

namespace DM.Domain.Game.Features.PostReviews;

public class CreatePostReviewEntity
{
    public Guid PostReviewId { get; set; }

    public Guid AuthorId { get; set; }

    public Guid PostId { get; set; }

    /// <summary>
    /// Denormalized for filtering
    /// </summary>
    public Guid PostAuthorId { get; set; }

    /// <summary>
    /// Denormalized for filtering
    /// </summary>
    public Guid GameId { get; set; }

    public DateTimeOffset CreatedUtc { get; set; }

    public ReviewSign Sign { get; set; }

    public string Text { get; set; } = null!;
}

/// <summary>
/// A null field is left unchanged
/// </summary>
public record UpdatePostReviewEntity(
    Guid PostReviewId,
    ReviewSign? Sign = null,
    string? Text = null,
    bool? IsRemoved = null,
    DateTimeOffset? ModifiedUtc = null,
    Guid? ModifiedByUserId = null,
    DateTimeOffset? DeletedUtc = null,
    Guid? DeletedByUserId = null);
