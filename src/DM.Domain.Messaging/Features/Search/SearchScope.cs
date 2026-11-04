using System;

namespace DM.Domain.Messaging.Features.Search;

/// <summary>
/// A single parsed "in:" scope. Access is always re-validated by the reused
/// access predicates, so an unresolvable/forged id simply yields no rows.
/// </summary>
public class SearchScope
{
    public SearchSourceType Type { get; set; }

    /// <summary>Resolved container id, when the token was a Guid.</summary>
    public Guid? Id { get; set; }

    /// <summary>Raw token (public id / slug) when not a Guid; resolved in the repository.</summary>
    public string? RawId { get; set; }
}
