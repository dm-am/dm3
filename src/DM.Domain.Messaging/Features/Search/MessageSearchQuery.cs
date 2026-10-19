using System;
using System.Collections.Generic;
using DM.Domain.Core.Dto;

namespace DM.Domain.Messaging.Features.Search;

/// <summary>
/// Fully-parsed unified message/post search query consumed by the repository.
/// Operator tokens (from:/before:/after:/during:/in:) are already extracted.
/// </summary>
public class MessageSearchQuery
{
    /// <summary>
    /// Free-text portion passed to websearch_to_tsquery (operators stripped).
    /// </summary>
    public string Text { get; set; } = "";

    /// <summary>Optional author username filter (case-insensitive).</summary>
    public string? FromUsername { get; set; }

    /// <summary>Inclusive lower bound on CreatedUtc.</summary>
    public DateTimeOffset? After { get; set; }

    /// <summary>Inclusive upper bound on CreatedUtc.</summary>
    public DateTimeOffset? Before { get; set; }

    /// <summary>
    /// Requested scopes. Empty means "all sources the caller can access".
    /// </summary>
    public IReadOnlyList<SearchScope> Scopes { get; set; } = [];

    /// <summary>
    /// Authors whose general chat messages are left out, as the general chat itself
    /// leaves them out: filled by the service, null when nothing is hidden.
    /// </summary>
    public IReadOnlyCollection<Guid>? HiddenAuthorIds { get; set; }

    /// <summary>Opaque keyset cursor for the next (older) page.</summary>
    public string? Cursor { get; set; }

    /// <summary>Requested page size (clamped).</summary>
    public int Limit { get; set; } = CursorQuery.DefaultLimit;

    /// <summary>Effective page size, clamped to the valid range.</summary>
    public int EffectiveLimit => CursorQuery.Clamp(Limit);
}
