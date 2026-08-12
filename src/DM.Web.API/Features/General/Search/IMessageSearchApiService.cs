using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.General.Search;

/// <summary>
/// API-facing service for the unified message/post full-text search.
/// </summary>
public interface IMessageSearchApiService
{
    /// <summary>
    /// Run a unified search for the current user.
    /// </summary>
    /// <param name="q">Query string (may contain inline operators).</param>
    /// <param name="scopes">Structured "in:" scope tokens.</param>
    /// <param name="from">Author username filter.</param>
    /// <param name="after">Inclusive lower bound on CreatedUtc.</param>
    /// <param name="before">Inclusive upper bound on CreatedUtc.</param>
    /// <param name="cursor">Opaque keyset cursor.</param>
    /// <param name="limit">Page size (1-100).</param>
    Task<CursorEnvelope<MessageSearchResult>> SearchAsync(
        string q,
        IReadOnlyList<string> scopes,
        string? from,
        DateTimeOffset? after,
        DateTimeOffset? before,
        string? cursor,
        int limit);
}
