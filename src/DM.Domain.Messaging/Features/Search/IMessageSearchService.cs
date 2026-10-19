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
    /// Search for the current user; the inline operators of the request are parsed here
    /// </summary>
    Task<CursorResult<MessageSearchHit>> SearchAsync(MessageSearchRequest request, CancellationToken ct = default);
}
