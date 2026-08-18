using DM.Domain.Core.Dto;

namespace DM.Web.API.Features.Game.Reviews;

/// <summary>
/// Paging, search and sorting of one user's game reviews — the profile
/// listings, received and written.
/// </summary>
/// <remarks>
/// The shape mirrors <see cref="Community.Endorsements.UserEndorsementsQuery" />:
/// the two profile pages are the same kind of list and are filtered the same
/// way, so an unknown parameter here would be a difference the reader has to
/// discover rather than a feature.
/// </remarks>
public class UserGameReviewsQuery : PagingQuery
{
    /// <summary>
    /// Substring search (case-insensitive) over the review text, author name
    /// and game title. Empty = no search.
    /// </summary>
    public string? Search { get; set; }

    /// <summary>
    /// Sort field: <c>created</c> or <c>author</c>. Default — created.
    /// <c>author</c> sorts by the other side of the pair: for "received" —
    /// by the review's author, for "written" — by the game's title.
    /// </summary>
    public string? SortBy { get; set; }

    /// <summary>Sort direction: <c>asc</c> or <c>desc</c>. Default — desc.</summary>
    public string? SortOrder { get; set; }
}
