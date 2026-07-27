using System;

namespace DM.Domain.Community.Features.UserEndorsements;

/// <summary>
/// Filter / search / sort parameters for endorsements.
/// Used identically for the "received" and "given" listings —
/// the only difference is which of <see cref="AuthorId"/> / <see cref="RecipientId"/>
/// is set by the controller.
/// </summary>
public class UserEndorsementFilter
{
    /// <summary>Filter by endorsement author.</summary>
    public Guid? AuthorId { get; set; }

    /// <summary>Filter by endorsement recipient (target user).</summary>
    public Guid? RecipientId { get; set; }

    /// <summary>
    /// Substring search (case-insensitive) over the endorsement text,
    /// author name and recipient name. Empty = no search.
    /// </summary>
    public string? Search { get; set; }

    /// <summary>
    /// Sort field: <c>created</c> (by date) or <c>author</c>
    /// (alphabetically by counterparty name: for "received" — the author,
    /// for "given" — the recipient). Default — created.
    /// </summary>
    public string? SortBy { get; set; }

    /// <summary>
    /// Sort direction: <c>asc</c> or <c>desc</c>. Default — desc.
    /// </summary>
    public string? SortOrder { get; set; }
}
