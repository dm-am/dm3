using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;

namespace DM.Domain.Messaging.Features.Search;

/// <summary>
/// Postgres tsvector-backed unified search over messages and game posts.
/// Access is re-derived from the supplied user id on every call.
/// </summary>
public interface IMessageSearchRepository
{
    /// <summary>
    /// Run a keyset-paginated full-text search across every source the user
    /// may read, restricted by the query's scopes/operators.
    /// </summary>
    /// <param name="userId">Authenticated user identifier (access is derived from it).</param>
    /// <param name="query">Parsed search query.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<CursorResult<MessageSearchHit>> Search(Guid userId, MessageSearchQuery query, CancellationToken ct = default);
}
