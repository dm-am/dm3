using System;
using DM.Domain.Core.Enums;

namespace DM.Domain.Game.Features.PostReviews;

public class UpdatePostReview
{
    public Guid ReviewId { get; set; }

    /// <summary>
    /// Null keeps the current one
    /// </summary>
    public ReviewSign? Sign { get; set; }

    /// <summary>
    /// Raw BBCode; null keeps the current one
    /// </summary>
    public string? Text { get; set; }
}
