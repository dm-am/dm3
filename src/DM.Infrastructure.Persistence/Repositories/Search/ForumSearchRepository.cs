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
    private const string SearchConfig = "russian";
    private const string TopicEntityType = "topic";
    private const string CommentEntityType = "comment";

    private readonly DmDbContext _dbContext;

    public ForumSearchRepository(DmDbContext dbContext)
    {
        _dbContext = dbContext;
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
                Snippet = t.Text,
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
                Snippet = c.Text,
                Rank = EF.Property<NpgsqlTsVector>(c, "SearchVector")
                    .Rank(EF.Functions.WebSearchToTsQuery(SearchConfig, query)),
            });

        var combined = topicHits.Concat(commentHits);

        var totalCount = await combined.CountAsync(ct);
        if (totalCount == 0)
        {
            return ([], 0);
        }

        var rows = await combined
            .OrderByDescending(h => h.Rank)
            .ThenByDescending(h => h.CreatedUtc)
            .ThenBy(h => h.Id)
            .Skip(pagingData.Skip)
            .Take(pagingData.Take)
            .ToArrayAsync(ct);

        foreach (var row in rows)
        {
            row.Snippet = SearchSnippet.Truncate(SearchSnippet.StripPrivateBlocks(row.Snippet));
        }

        return (rows, totalCount);
    }
}
