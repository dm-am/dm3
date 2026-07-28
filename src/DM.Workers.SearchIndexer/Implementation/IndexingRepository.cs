using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Search;
using OpenSearch.Client;

namespace DM.Workers.SearchIndexer.Implementation;

/// <inheritdoc />
internal class IndexingRepository(IOpenSearchClient client) : IIndexingRepository
{
    public async Task Index(params SearchEntity[] entities)
    {
        await DeclareIndex();
        Ensure(await client.IndexManyAsync(entities));
    }

    public async Task Delete(Guid entityId)
    {
        var response = await client.DeleteAsync<SearchEntity>(entityId);
        // Deleting an absent document is the desired end state, not a failure:
        // the delete event may arrive after the document was already dropped.
        if (response.ApiCall?.HttpStatusCode == (int)HttpStatusCode.NotFound)
        {
            return;
        }

        Ensure(response);
    }

    public async Task DeleteByParent(Guid parentEntityId) =>
        Ensure(await client.DeleteByQueryAsync<SearchEntity>(d => d.Query(q => q
            .Term(t => t
                .Field(f => f.ParentEntityId)
                .Value(parentEntityId)))));

    public async Task UpdateByParent(Guid parentEntityId, IEnumerable<UserRole> roles) =>
        Ensure(await client.UpdateByQueryAsync<SearchEntity>(d => d.Query(q => q
                .Term(t => t
                    .Field(f => f.ParentEntityId)
                    .Value(parentEntityId)))
            .Script(s => s
                .Source("ctx._source.authorizedRoles = params.roles")
                .Params(p => p.Add("roles", roles.Cast<int>().ToArray())))));

    private async Task DeclareIndex()
    {
        var existsResponse = await client.Indices.ExistsAsync(SearchEngineConfiguration.IndexName);
        if (existsResponse is { IsValid: true, Exists: true })
        {
            return;
        }

        var createResponse = await client.Indices.CreateAsync(SearchEngineConfiguration.IndexName, i => i
            .Settings(s => s
                .Analysis(a => a
                    .Analyzers(an => an
                        .Custom("dm_analyzer", ca => ca
                            .CharFilters("html_strip")
                            .Tokenizer("standard")
                            .Filters("standard", "lowercase", "stop"))
                        .Custom("dm_search_analyzer", ca => ca
                            .Tokenizer("standard")
                            .Filters("standard", "lowercase", "stop")))))
            .Map<SearchEntity>(m => m
                .AutoMap()
                .Properties(p => p
                    .Text(t => t
                        .Name(n => n.Text)
                        .Analyzer("dm_analyzer")
                        .SearchAnalyzer("dm_search_analyzer")))));

        // Two workers racing on the first indexed entity both see "not exists"
        // and both create; the loser gets this and is already in the state it
        // wanted.
        if (createResponse.ServerError?.Error?.Type == "resource_already_exists_exception")
        {
            return;
        }

        Ensure(createResponse);
    }

    // Every mutating call is checked. An unchecked OpenSearch response means a
    // rejected write is indistinguishable from a successful one, and the index
    // silently drifts from the database with nothing to point at. Throwing
    // hands the decision to the consumer retry middleware.
    private static void Ensure(IResponse response)
    {
        if (response.IsValid)
        {
            return;
        }

        throw new InvalidOperationException(
            "OpenSearch request failed: " + response.DebugInformation,
            response.OriginalException);
    }
}
