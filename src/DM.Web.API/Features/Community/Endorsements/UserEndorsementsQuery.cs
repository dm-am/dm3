using DM.Domain.Core.Dto;

namespace DM.Web.API.Features.Community.Endorsements;

/// <summary>
/// Search / sort / paging for the endorsement GET endpoints
/// (both received and given).
/// </summary>
public class UserEndorsementsQuery : PagingQuery
{
    /// <summary>
    /// Substring search (case-insensitive) over the endorsement text,
    /// author name and recipient name. Empty = no search.
    /// </summary>
    public string? Search { get; set; }

    /// <summary>
    /// Sort field: <c>created</c> or <c>author</c>. Default — created.
    /// <c>author</c> sorts by counterparty name: for "received" —
    /// by the author, for "given" — by the recipient.
    /// </summary>
    public string? SortBy { get; set; }

    /// <summary>Sort direction: <c>asc</c> or <c>desc</c>. Default — desc.</summary>
    public string? SortOrder { get; set; }
}
