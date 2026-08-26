using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Forum.Features.Boards;
using DM.Infrastructure.Persistence.Shared.Queries;
using Microsoft.EntityFrameworkCore;

namespace DM.Infrastructure.Persistence.Repositories.Forum;

/// <inheritdoc />
internal class BoardRepository(
    DmDbContext dmDbContext)
    : IBoardRepository
{
    /// <inheritdoc />
    /// <remarks>
    /// Deliberately uncached. The board list used to live in a process cache with
    /// an hour-long TTL that nothing invalidated, which made every counter on the
    /// forum index up to an hour stale — and the per-user unread counts were
    /// written straight into those shared instances, so one reader's numbers could
    /// surface for another. There are eleven boards: reading them fresh costs two
    /// queries over a fixed, tiny set, and in exchange a counter always tells the
    /// truth at the moment it is rendered.
    /// </remarks>
    public async Task<IEnumerable<Board>> SelectBoards(BoardAccessPolicy? accessPolicy)
    {
        var boards = await dmDbContext.Boards
            .TagWith("DM.Forum.BoardsList")
            .OrderBy(b => b.Order)
            .ProjectToBoard()
            .ToArrayAsync();

        await FillCommentSummary(boards);

        if (!accessPolicy.HasValue)
        {
            return boards;
        }

        return boards.Where(b => (b.ViewPolicy & accessPolicy) != BoardAccessPolicy.None).ToArray();
    }

    /// <summary>
    /// Compute the comment count and the last comment for each board.
    /// </summary>
    /// <remarks>
    /// Comments are polymorphic by EntityId with no type column, so the join to
    /// Topics is what restricts the set to forum comments. Two statements rather
    /// than one: a per-group "top 1" does not translate reliably, so the aggregate
    /// produces the newest timestamp per board and the second query fetches the
    /// rows behind those timestamps.
    /// </remarks>
    private async Task FillCommentSummary(IReadOnlyCollection<Board> boards)
    {
        if (boards.Count == 0)
        {
            return;
        }

        var boardIds = boards.Select(b => b.Id).ToHashSet();

        var summaries = await dmDbContext.Comments
            .TagWith("DM.Forum.BoardCommentSummary")
            .Join(dmDbContext.Topics, c => c.EntityId, t => t.TopicId, (c, t) => new { t.BoardId, c.CreatedUtc })
            .Where(x => boardIds.Contains(x.BoardId))
            .GroupBy(x => x.BoardId)
            .Select(g => new { BoardId = g.Key, Count = g.Count(), LastUtc = g.Max(x => x.CreatedUtc) })
            .ToDictionaryAsync(x => x.BoardId, x => x);

        var lastUtcs = summaries.Values.Select(s => s.LastUtc).Distinct().ToArray();
        var lastByBoard = new Dictionary<System.Guid, BoardLastComment>();

        if (lastUtcs.Length > 0)
        {
            var lastComments = await dmDbContext.Comments
                .TagWith("DM.Forum.BoardLastComment")
                .Join(dmDbContext.Topics, c => c.EntityId, t => t.TopicId, (c, t) => new { Comment = c, Topic = t })
                .Where(x => boardIds.Contains(x.Topic.BoardId) && lastUtcs.Contains(x.Comment.CreatedUtc))
                .SelectSpliced(x => new
                {
                    x.Topic.BoardId,
                    Last = new BoardLastComment
                    {
                        Id = x.Comment.CommentId,
                        TopicId = x.Topic.TopicId,
                        TopicTitle = x.Topic.Title,
                        TopicNumber = x.Topic.TopicNumber,
                        CreatedUtc = x.Comment.CreatedUtc,
                        // The same narrow stub the board list itself renders.
                        Author = ForumMappers.BoardUserStub.Splice(x.Comment.Author),
                    },
                })
                .ToArrayAsync();

            // The identifier breaks the tie: two comments can share a timestamp — an
            // import produces that by the thousand — and without it the board's last
            // comment is whichever row the plan returned first, which can differ
            // between two renders of the same unchanged board.
            lastByBoard = lastComments
                .GroupBy(x => x.BoardId)
                .ToDictionary(g => g.Key, g => g
                    .OrderByDescending(x => x.Last.CreatedUtc)
                    .ThenByDescending(x => x.Last.Id)
                    .First().Last);
        }

        foreach (var board in boards)
        {
            board.CommentsCount = summaries.TryGetValue(board.Id, out var summary) ? summary.Count : 0;
            board.LastComment = lastByBoard.GetValueOrDefault(board.Id);
        }
    }
}
