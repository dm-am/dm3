using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Forum.Features.Search;
using Microsoft.EntityFrameworkCore;
using NpgsqlTypes;

namespace DM.Infrastructure.Persistence.Repositories.Search;

/// <summary>
/// Postgres tsvector-backed full-text search over forum topics and comments.
/// UNION ALL of {visible topics} + {comments on visible topics}, ranked by
/// ts_rank, with visibility re-derived from the caller's access policy on
/// every page.
/// </summary>
internal class ForumSearchRepository : IForumSearchRepository
{
    private const string TopicEntityType = "topic";
    private const string CommentEntityType = "comment";

    private readonly DmDbContext _dbContext;

    public ForumSearchRepository(DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Fills in the preview of a page: a window around the match, with the match marked.
    /// </summary>
    /// <remarks>
    /// The forum surface does not declare the hidden block, yet the window cuts it as the
    /// message search does: the two answer the same question over one column.
    /// </remarks>
    private async Task Preview(ForumSearchHit[] page, string query, CancellationToken ct)
    {
        var topicIds = page.Where(h => h.EntityType == TopicEntityType).Select(h => h.Id).ToArray();
        var commentIds = page.Where(h => h.EntityType == CommentEntityType).Select(h => h.Id).ToArray();

        var headlines = await SearchSnippet.Headlines(query, ct,
            (topicIds, _dbContext.Topics
                .Where(t => topicIds.Contains(t.TopicId))
                .Select(t => new SearchSnippet.Body { Id = t.TopicId, Text = EF.Property<string>(t, "SearchText") })),
            (commentIds, _dbContext.Comments
                .Where(c => commentIds.Contains(c.CommentId))
                .Select(c => new SearchSnippet.Body { Id = c.CommentId, Text = EF.Property<string>(c, "SearchText") })));

        foreach (var row in page)
        {
            row.SnippetSegments = headlines.TryGetValue(row.Id, out var headline)
                ? SearchSnippet.Highlight(headline)
                : SearchSnippet.Plain(row.Snippet);
        }
    }

    /// <inheritdoc />
    public async Task<(IEnumerable<ForumSearchHit> hits, int totalCount)> Search(
        string query, PagingData pagingData, CancellationToken ct = default)
    {
        // Soft-deleted rows are already excluded by the global query filter on
        // IRemovable, so neither branch repeats the condition.
        var topicHits = _dbContext.Topics
            .Where(t => EF.Property<NpgsqlTsVector>(t, "SearchVector")
                .Matches(EF.Functions.WebSearchToTsQuery(SearchTextConfiguration.Name, query)))
            .Select(t => new ForumSearchHit
            {
                EntityType = TopicEntityType,
                Id = t.TopicId,
                TopicId = t.TopicId,
                TopicNumber = t.TopicNumber,
                TopicTitle = t.Title,
                BoardId = t.BoardId,
                BoardTitle = t.Board.Title,
                CreatedUtc = t.CreatedUtc,
                Snippet = EF.Property<string>(t, "SearchText"),
                Rank = EF.Property<NpgsqlTsVector>(t, "SearchVector")
                    .Rank(EF.Functions.WebSearchToTsQuery(SearchTextConfiguration.Name, query)),
            });

        // Comment.EntityId is polymorphic and the table carries no type column,
        // so this join is the only thing that makes the branch forum-only: a
        // comment whose EntityId is a game, blog or publication matches nothing
        // in Topics and drops out.
        var commentHits = _dbContext.Comments
            .Where(c => EF.Property<NpgsqlTsVector>(c, "SearchVector")
                .Matches(EF.Functions.WebSearchToTsQuery(SearchTextConfiguration.Name, query)))
            .Join(_dbContext.Topics, c => c.EntityId, t => t.TopicId, (c, t) => new ForumSearchHit
            {
                EntityType = CommentEntityType,
                Id = c.CommentId,
                TopicId = t.TopicId,
                TopicNumber = t.TopicNumber,
                TopicTitle = t.Title,
                BoardId = t.BoardId,
                BoardTitle = t.Board.Title,
                CreatedUtc = c.CreatedUtc,
                Snippet = EF.Property<string>(c, "SearchText"),
                Rank = EF.Property<NpgsqlTsVector>(c, "SearchVector")
                    .Rank(EF.Functions.WebSearchToTsQuery(SearchTextConfiguration.Name, query)),
            });

        var combined = topicHits.Concat(commentHits);

        var totalCount = await combined.CountAsync(ct);
        if (totalCount == 0)
        {
            return ([], 0);
        }

        // ts_rank runs with the default normalisation flag, which ignores the
        // length of the document: rank grows with the number of occurrences alone,
        // so a long topic repeating the term outranks a short one about it, and
        // equal ranks fall through to recency below. Picking a flag blind would
        // also reorder against the A and B weights the topic vector already pays
        // for - a decision worth making when this endpoint has a UI and a corpus
        // to look at, and not before.
        var rows = await combined
            .OrderByDescending(h => h.Rank)
            .ThenByDescending(h => h.CreatedUtc)
            .ThenBy(h => h.Id)
            .Skip(pagingData.Skip)
            .Take(pagingData.Take)
            .ToArrayAsync(ct);

        await Preview(rows, query, ct);

        return (rows, totalCount);
    }
}
