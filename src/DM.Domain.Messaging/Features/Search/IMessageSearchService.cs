using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;

namespace DM.Domain.Messaging.Features.Search;

/// <summary>
/// Unified full-text search over global chat, participant chats and readable
/// game posts. Parses operator tokens and re-validates access per request.
/// </summary>
public interface IMessageSearchService
{
    /// <summary>
    /// Execute a unified search for the current authenticated user.
    /// </summary>
    /// <param name="request">Raw request (inline operators are parsed here).</param>
    /// <param name="ct">Cancellation token.</param>
    Task<CursorResult<MessageSearchHit>> SearchAsync(MessageSearchRequest request, CancellationToken ct = default);
}
