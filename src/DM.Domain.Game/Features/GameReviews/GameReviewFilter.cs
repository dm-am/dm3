using System;

namespace DM.Domain.Game.Features.GameReviews;

public class GameReviewFilter
{
    public Guid? AuthorId { get; set; }

    public Guid? GameId { get; set; }

    /// <summary>
    /// Games this user masters
    /// </summary>
    public Guid? GmId { get; set; }

    /// <summary>
    /// Case-insensitive substring of the text, the author name or the game title
    /// </summary>
    public string? Search { get; set; }

    /// <summary>
    /// <c>created</c> (default) or <c>author</c>: the other side of the pair, the author for reviews
    /// received and the game for reviews written.
    /// </summary>
    public string? SortBy { get; set; }

    /// <summary>
    /// <c>asc</c> or <c>desc</c> (default)
    /// </summary>
    public string? SortOrder { get; set; }
}
