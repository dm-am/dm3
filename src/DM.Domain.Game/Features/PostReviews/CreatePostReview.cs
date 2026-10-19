using System;
using DM.Domain.Core.Enums;

namespace DM.Domain.Game.Features.PostReviews;

public class CreatePostReview
{
    public Guid PostId { get; set; }

    public ReviewSign Sign { get; set; }

    /// <summary>
    /// Required, BBCode
    /// </summary>
    public string Text { get; set; } = null!;
}
