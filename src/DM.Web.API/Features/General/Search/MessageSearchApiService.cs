using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Messaging.Features.Search;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.General.Search;

/// <inheritdoc />
internal class MessageSearchApiService : IMessageSearchApiService
{
    private readonly IMessageSearchService _searchService;

    public MessageSearchApiService(IMessageSearchService searchService)
    {
        _searchService = searchService;
    }

    /// <inheritdoc />
    public async Task<CursorEnvelope<MessageSearchResult>> SearchAsync(
        string q,
        IReadOnlyList<string> scopes,
        string? from,
        DateTimeOffset? after,
        DateTimeOffset? before,
        string? cursor,
        int limit)
    {
        var request = new MessageSearchRequest
        {
            Query = q,
            In = scopes ?? Array.Empty<string>(),
            From = from,
            After = after,
            Before = before,
            Cursor = cursor,
            Limit = limit
        };

        var result = await _searchService.SearchAsync(request);

        var paging = new CursorPaging
        {
            NextCursor = result.NextCursor,
            PrevCursor = result.PrevCursor,
            HasNext = result.HasNext,
            HasPrev = result.HasPrev
        };

        var rows = result.Data.Select(h => new MessageSearchResult
        {
            SourceType = h.SourceType,
            SourceId = h.SourceId,
            SourceTitle = h.SourceTitle,
            Id = h.Id,
            CreatedUtc = h.CreatedUtc,
            Snippet = h.SnippetSegments
        });

        return new CursorEnvelope<MessageSearchResult>(rows, paging);
    }
}
