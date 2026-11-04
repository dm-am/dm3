using System;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;

namespace DM.Domain.Game.Features.PostReviews;

/// <remarks>
/// One review per author and post.
/// </remarks>
public class PostReview
{
    public Guid Id { get; set; }

    public GeneralUser Author { get; set; } = null!;

    public Guid PostId { get; set; }

    public GeneralUser PostAuthor { get; set; } = null!;

    public Guid GameId { get; set; }

    public DateTimeOffset CreatedUtc { get; set; }

    public DateTimeOffset? ModifiedUtc { get; set; }

    /// <summary>
    /// BBCode
    /// </summary>
    public string? Text { get; set; }

    public ReviewSign Sign { get; set; }
}
