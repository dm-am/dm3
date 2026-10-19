using System;

namespace DM.Domain.Game.Features.PostReviews;

public class PostInfo
{
    public Guid AuthorId { get; set; }

    public Guid GameId { get; set; }
}
