using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
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
    private const string SearchConfig = SearchTextConfiguration.Name;
    private const string TopicEntityType = "topic";
    private const string CommentEntityType = "comment";

    private readonly DmDbContext _dbContext;

    public ForumSearchRepository(DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Fills in the preview of a page: a window around the match, with the match
    /// marked.
    /// </summary>
    /// <remarks>
    /// A second pass over the page rather than part of the projection above, and
    /// not for tidiness. ts_headline re-parses the document it is given, and in the
    /// select list of the ranked query it would run for every row that matched
    /// anywhere in the forum - thousands of them - to be thrown away by the paging
    /// a moment later. Here it runs once per row a reader will actually see.
    ///
    /// The window is what makes the preview worth reading. Cut from the beginning
    /// instead, it showed the first two hundred characters of a topic and almost
    /// never the words that were searched for; and it could not have found them by
    /// looking, because the search matches by lexeme - a query for "странник"
    /// matches "странников", which no substring of the query occurs in.
    /// </remarks>
    private async Task Preview(ForumSearchHit[] page, string query, CancellationToken ct)
    {
        if (page.Length == 0)
        {
            return;
        }

        // A filter with no words in it - by author, by date - matches every row
        // equally, and there is nothing to build a window around.
        if (string.IsNullOrWhiteSpace(query))
        {
            foreach (var row in page)
            {
                row.SnippetSegments = SearchSnippet.Plain(row.Snippet);
            }

            return;
        }

        var topicIds = page.Where(h => h.EntityType == TopicEntityType).Select(h => h.Id).ToArray();
        var commentIds = page.Where(h => h.EntityType == CommentEntityType).Select(h => h.Id).ToArray();

        var headlines = new Dictionary<Guid, string>();

        if (topicIds.Length > 0)
        {
            foreach (var row in await _dbContext.Topics
                .Where(t => topicIds.Contains(t.TopicId))
                .Select(t => new
                {
                    t.TopicId,
                    Headline = EF.Functions.WebSearchToTsQuery(SearchConfig, query)
                        .GetResultHeadline(SearchConfig, EF.Property<string>(t, "SearchText"), SearchSnippet.HeadlineOptions),
                })
                .ToArrayAsync(ct))
            {
                headlines[row.TopicId] = row.Headline;
            }
        }

        if (commentIds.Length > 0)
        {
            foreach (var row in await _dbContext.Comments
                .Where(c => commentIds.Contains(c.CommentId))
                .Select(c => new
                {
                    c.CommentId,
                    Headline = EF.Functions.WebSearchToTsQuery(SearchConfig, query)
                        .GetResultHeadline(SearchConfig, EF.Property<string>(c, "SearchText"), SearchSnippet.HeadlineOptions),
                })
                .ToArrayAsync(ct))
            {
                headlines[row.CommentId] = row.Headline;
            }
        }

        foreach (var row in page)
        {
            // StripPrivateBlocks over the fallback stays as a second lock over the
            // projection, which already removed the block: the column cannot fail
            // and a process can.
            row.SnippetSegments = headlines.TryGetValue(row.Id, out var headline)
                ? SearchSnippet.Highlight(headline)
                : SearchSnippet.Plain(row.Snippet);
        }
    }

    /// <inheritdoc />
    public async Task<(IEnumerable<ForumSearchHit> hits, int totalCount)> Search(
        string query, BoardAccessPolicy accessPolicy, PagingData pagingData, CancellationToken ct = default)
    {
        // Soft-deleted rows are already excluded by the global query filter on
        // IRemovable, so neither branch repeats the condition.
        var visibleTopics = _dbContext.Topics
            .Where(t => (t.Board.ViewPolicy & accessPolicy) != BoardAccessPolicy.None);

        var topicHits = visibleTopics
            .Where(t => EF.Property<NpgsqlTsVector>(t, "SearchVector")
                .Matches(EF.Functions.WebSearchToTsQuery(SearchConfig, query)))
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
                    .Rank(EF.Functions.WebSearchToTsQuery(SearchConfig, query)),
            });

        // Comment.EntityId is polymorphic and the table carries no type column,
        // so this join is the only thing that makes the branch forum-only: a
        // comment whose EntityId is a game, blog or publication matches nothing
        // in Topics and drops out.
        var commentHits = _dbContext.Comments
            .Where(c => EF.Property<NpgsqlTsVector>(c, "SearchVector")
                .Matches(EF.Functions.WebSearchToTsQuery(SearchConfig, query)))
            .Join(visibleTopics, c => c.EntityId, t => t.TopicId, (c, t) => new ForumSearchHit
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
                    .Rank(EF.Functions.WebSearchToTsQuery(SearchConfig, query)),
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
