using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Search;
using Microsoft.Extensions.Logging;
using OpenSearch.Client;

namespace DM.Infrastructure.Core.Search;

/// <inheritdoc />
internal class SearchEngineRepository : ISearchEngineRepository
{
    private readonly IOpenSearchClient _client;
    private readonly ILogger<SearchEngineRepository> _logger;

    /// <inheritdoc />
    public SearchEngineRepository(
        IOpenSearchClient client,
        ILogger<SearchEngineRepository> logger)
    {
        _client = client;
        _logger = logger;
    }

    private static readonly Fuzziness SearchFuzziness = Fuzziness.EditDistance(1);

    /// <inheritdoc />
    public async Task<(IEnumerable<FoundEntity> entities, int totalCount)> Search(string query,
        IEnumerable<SearchEntityType> types, PagingData pagingData, IEnumerable<UserRole> roles, Guid userId)
    {
        var searchResponse = await _client.SearchAsync<SearchEntity>(s => s
            .Source(sf => sf.Excludes(e => e.Fields(
                f => f.AuthorizedRoles, f => f.AuthorizedUsers, f => f.UnauthorizedUsers)))
            .Query(q =>
            {
                var querySearch =
                    q.Match(mt => mt.Field(f => f.Text)
                        .Query(query)
                        .Fuzziness(SearchFuzziness)
                        .Boost(1)) ||
                    q.Match(mt => mt.Field(f => f.Title)
                        .Query(query)
                        .Fuzziness(SearchFuzziness)
                        .Boost(3)) ||
                    q.Prefix(pr => pr.Field(f => f.Title)
                        .Value(query)
                        .Boost(2));

                var searchEntityTypes = types as SearchEntityType[] ?? types.ToArray();
                if (searchEntityTypes.Any())
                {
                    querySearch = querySearch && q.Terms(t => t.Field(f => f.EntityType).Terms(searchEntityTypes));
                }

                // Granted by role OR by explicit user, AND not explicitly denied.
                // The last clause used to be a third alternative, and since nothing
                // ever writes UnauthorizedUsers it was true for every document —
                // which made the whole expression true for every document, so the
                // filter matched everything and restricted content was returned to
                // anyone, including anonymous callers.
                var authorizeSearch =
                    (q.Terms(t => t.Field(f => f.AuthorizedRoles).Terms(roles.Cast<int>())) ||
                        q.Terms(t => t.Field(f => f.AuthorizedUsers).Terms(userId))) &&
                    !q.Terms(t => t.Field(f => f.UnauthorizedUsers).Terms(userId));

                return querySearch && authorizeSearch;
            })
            .Sort(so => so.Descending(SortSpecialField.Score))
            .Highlight(h => h
                .Fields(
                    f => f
                        .Field(ff => ff.Title)
                        .PreTags("<mark>")
                        .PostTags("</mark>"),
                    f => f
                        .Field(ff => ff.Text)
                        .PreTags("<mark>")
                        .PostTags("</mark>")))
            .From(pagingData.Skip)
            .Size(pagingData.Take));

        if (searchResponse is not { IsValid: true })
        {
            _logger.LogError(searchResponse.OriginalException,
                "The search for query {SearchQuery} has resulted in error", query);
            throw new HttpException(HttpStatusCode.InternalServerError, "Search engine error!");
        }

        return (searchResponse.Hits
            .Select(h => new FoundEntity
            {
                Id = h.Source.Id,
                Type = h.Source.EntityType,
                FoundTitle = h.Highlight.TryGetValue(nameof(SearchEntity.Title).ToLower(), out var titleHits)
                    ? titleHits.First()
                    : h.Source.Title,
                OriginalTitle = h.Source.Title,
                FoundText = h.Highlight.TryGetValue(nameof(SearchEntity.Text).ToLower(), out var textHits)
                    ? string.Join("<br />", textHits.Where(hl => !string.IsNullOrWhiteSpace(hl)))
                    : h.Source.Text
            }), (int) searchResponse.Total);
    }
}
