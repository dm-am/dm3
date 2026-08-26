using System;
using System.Collections.Generic;
using DM.Domain.Core.Dto;

namespace DM.Domain.Messaging.Features.Search;

/// <summary>
/// Source kind a search scope refers to.
/// </summary>
public enum SearchSourceType
{
    /// <summary>Global chat.</summary>
    Global,

    /// <summary>A specific direct/group chat.</summary>
    Chat,

    /// <summary>A specific game (its readable posts).</summary>
    Game
}

/// <summary>
/// A single parsed "in:" scope. Access is always re-validated by the reused
/// access predicates, so an unresolvable/forged id simply yields no rows.
/// </summary>
public class SearchScope
{
    /// <summary>Scope kind.</summary>
    public SearchSourceType Type { get; set; }

    /// <summary>Resolved container id, when the token was a Guid.</summary>
    public Guid? Id { get; set; }

    /// <summary>Raw token (public id / slug) when not a Guid; resolved in the repository.</summary>
    public string? RawId { get; set; }
}

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
    public IReadOnlyList<SearchScope> Scopes { get; set; } = new List<SearchScope>();

    /// <summary>Opaque keyset cursor for the next (older) page.</summary>
    public string? Cursor { get; set; }

    /// <summary>Requested page size (clamped).</summary>
    public int Limit { get; set; } = CursorQuery.DefaultLimit;

    /// <summary>Effective page size, clamped to the valid range.</summary>
    public int EffectiveLimit => CursorQuery.Clamp(Limit);
}
