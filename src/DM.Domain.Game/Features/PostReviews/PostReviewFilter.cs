using System;

namespace DM.Domain.Game.Features.PostReviews;

public class PostReviewFilter
{
    /// <summary>
    /// Reviews by this user
    /// </summary>
    public Guid? AuthorId { get; set; }

    /// <summary>
    /// Reviews of this user's posts
    /// </summary>
    public Guid? RecipientId { get; set; }

    public Guid? GameId { get; set; }
}
