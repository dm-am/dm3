using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Comments;
using DM.Domain.Core.Dto;
using DM.Domain.Forum.Features.Comments;
using DM.Infrastructure.Persistence.RelationalStorage;
using DM.Infrastructure.Persistence.Shared.Comments;
using Microsoft.EntityFrameworkCore;

namespace DM.Infrastructure.Persistence.Repositories.Forum;

/// <inheritdoc />
internal class TopicCommentRepository : ITopicCommentRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;

    public TopicCommentRepository(
        DmDbContext dbContext,
        IGuidFactory guidFactory,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _guidFactory = guidFactory;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc />
    public Task<int> Count(Guid topicId, CommentsQuery query, IReadOnlyCollection<Guid>? excludeUserIds = null) =>
        CommentQueries.Count(_dbContext, topicId, query, excludeUserIds, "DM.TopicComments.Count");

    /// <inheritdoc />
    public Task<IEnumerable<Comment>> Get(Guid topicId, CommentsQuery query, PagingData paging, IReadOnlyCollection<Guid>? excludeUserIds = null) =>
        CommentQueries.Page(_dbContext, topicId, query, paging, excludeUserIds, "DM.TopicComments.List");

    /// <inheritdoc />
    public Task<Comment?> Get(Guid commentId) =>
        CommentQueries.Single(_dbContext, commentId, CommentQueries.TopicIds(_dbContext), "DM.TopicComments.Get");

    /// <inheritdoc />
    public async Task<FirstUnreadComment?> FindFirstUnread(Guid topicId, DateTimeOffset lastReadUtc,
        IReadOnlyCollection<Guid>? excludeUserIds = null)
    {
        var comments = VisibleComments(topicId, excludeUserIds);

        var firstUnread = await comments
            .TagWith("DM.TopicComments.FirstUnread")
            .Where(c => c.CreatedUtc > lastReadUtc)
            .OrderBy(c => c.CreatedUtc)
            .Select(c => new { c.CommentId, c.CreatedUtc })
            .FirstOrDefaultAsync();

        return firstUnread == null
            ? null
            : await Position(comments, firstUnread.CommentId, firstUnread.CreatedUtc);
    }

    /// <inheritdoc />
    public async Task<FirstUnreadComment?> GetLastComment(Guid topicId,
        IReadOnlyCollection<Guid>? excludeUserIds = null)
    {
        var comments = VisibleComments(topicId, excludeUserIds);

        var lastComment = await comments
            .TagWith("DM.TopicComments.LastComment")
            .OrderByDescending(c => c.CreatedUtc)
            .Select(c => new { c.CommentId, c.CreatedUtc })
            .FirstOrDefaultAsync();

        return lastComment == null
            ? null
            : await Position(comments, lastComment.CommentId, lastComment.CreatedUtc);
    }

    /// <summary>
    /// Comments of the topic as this reader is shown them. Counting a position
    /// over any other set would send him to a page the comment is not on.
    /// </summary>
    private IQueryable<Entities.Shared.Comment> VisibleComments(
        Guid topicId, IReadOnlyCollection<Guid>? excludeUserIds) =>
        CommentQueries.WithoutAuthors(CommentQueries.Discussion(_dbContext, topicId), excludeUserIds);

    /// <summary>
    /// The comment's 1-based place in the order the discussion is paged by,
    /// oldest first, which is the order the list renders without a sort.
    /// </summary>
    private static async Task<FirstUnreadComment> Position(
        IQueryable<Entities.Shared.Comment> comments, Guid commentId, DateTimeOffset createdUtc)
    {
        return new FirstUnreadComment
        {
            CommentId = commentId,
            CommentNumber = await comments
                .TagWith("DM.TopicComments.Position")
                .CountAsync(c => c.CreatedUtc <= createdUtc)
        };
    }

    /// <summary>
    /// The topic's half of the last-comment move.
    /// </summary>
    /// <remarks>
    /// What the statement says and why it says it that way is stated once, on
    /// <see cref="CommentWrites.Create{TOwner}" />, and the other three discussions
    /// carry the same statement over their own table. Only the table is spelled out
    /// here: EF refuses raw SQL whose text is not a compile-time constant.
    /// </remarks>
    private const string MoveLastCommentForwardSql =
        """
        UPDATE "Topics" SET "LastCommentId" = {1}
        WHERE "TopicId" = {0}
          AND NOT EXISTS (
              SELECT 1 FROM "Comments" c
              WHERE c."CommentId" = "Topics"."LastCommentId"
                AND (c."CreatedUtc", c."CommentId") > ({2}, {1}))
        """;

    /// <summary>
    /// The topic row, held for the rest of the transaction.
    /// </summary>
    /// <remarks>
    /// The other three take their owner row through the statement that moves the
    /// discussion's comment count; a topic keeps no count, so it is taken by name.
    /// Taken as the first statement, before anything the pointer is decided on is
    /// read: a statement that waited on the row still answers from the snapshot it
    /// started with.
    /// </remarks>
    private const string LockTopicSql =
        """SELECT "TopicId" FROM "Topics" WHERE "TopicId" = {0} FOR UPDATE""";

    /// <inheritdoc />
    public async Task<Comment> Create(CreateTopicCommentEntity createComment)
    {
        var commentId = _guidFactory.Create();
        var now = _dateTimeProvider.Now;

        var dbComment = new Entities.Shared.Comment
        {
            CommentId = commentId,
            EntityId = createComment.TopicId,
            AuthorId = createComment.AuthorId,
            Text = createComment.Text,
            CreatedUtc = now,
            IsRemoved = false
        };

        // The pointer used to be assigned on the tracked topic row, unconditionally:
        // CreatedUtc is stamped before the write reaches the topic, so the comment
        // that commits last is not necessarily the newest one, and a reply that
        // waited on a lock or a retry sent the discussion's last activity backwards.
        // The forum list orders topics by that pointer.
        //
        // Both writes or neither, and through the strategy, for the reason stated on
        // RetryableWrite: the comment and the pointer are one unit.
        await RetryableWrite.Run(_dbContext, async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync();

            await _dbContext.Database.ExecuteSqlRawAsync(LockTopicSql, [createComment.TopicId]);

            _dbContext.Comments.Add(dbComment);
            await _dbContext.SaveChangesAsync();

            await _dbContext.Database.ExecuteSqlRawAsync(
                MoveLastCommentForwardSql, [createComment.TopicId, commentId, now]);

            await transaction.CommitAsync();
        });

        return await _dbContext.Comments
            .TagWith("DM.TopicComments.Created")
            .Where(c => c.CommentId == commentId)
            .ProjectToComment()
            .FirstAsync();
    }

    /// <inheritdoc />
    public Task<Comment> Update(UpdateTopicCommentEntity updateComment)
        => CommentWrites.Update(_dbContext, _guidFactory, updateComment.CommentId, updateComment.Text,
            updateComment.EditorUserId, updateComment.LastUpdateUtc, "DM.TopicComments.Updated");

    /// <inheritdoc />
    public Task<TopicCommentToDelete?> GetForDelete(Guid commentId) =>
        CommentQueries.One(_dbContext, commentId, CommentQueries.TopicIds(_dbContext))
        .TagWith("DM.TopicComments.GetForDelete")
        .ProjectToTopicCommentToDelete()
        .FirstOrDefaultAsync<TopicCommentToDelete?>();

    /// <inheritdoc />
    public async Task<bool> Delete(DeleteTopicCommentEntity deleteComment)
    {
        var removed = false;

        // Both writes or neither, and through the strategy, for the reason stated on
        // RetryableWrite. What the answer is for is stated on the interface.
        await RetryableWrite.Run(_dbContext, async () =>
        {
            // A replay runs the whole block again, so the answer of the attempt that
            // rolled back must not survive into it.
            removed = false;

            await using var transaction = await _dbContext.Database.BeginTransactionAsync();

            await _dbContext.Database.ExecuteSqlRawAsync(LockTopicSql, [deleteComment.TopicId]);

            // One statement reads and writes the comment row: a second removal of the
            // same comment arriving together with this one waits on the row lock and
            // then re-tests the predicate against the committed state, so exactly one
            // of the two matches.
            var marked = await _dbContext.Comments
                .Where(c => c.CommentId == deleteComment.CommentId && !c.IsRemoved)
                .ExecuteUpdateAsync(c => c
                    .SetProperty(x => x.IsRemoved, true)
                    .SetProperty(x => x.DeletedByUserId, (Guid?)deleteComment.DeletedByUserId)
                    .SetProperty(x => x.DeletedUtc, (DateTimeOffset?)deleteComment.DeletedUtc));

            removed = marked == 1;
            if (removed)
            {
                // The successor is chosen here, inside the transaction and after the
                // mark, rather than handed in by a caller that read it a statement
                // earlier: a comment written in between would otherwise be passed over
                // and the pointer would go back to a comment older than the
                // discussion's newest.
                var successor = await CommentQueries.NewestExcept(
                    _dbContext, deleteComment.TopicId, deleteComment.CommentId,
                    "DM.TopicComments.NewestCommentIdExcept");

                // The pointer moves only when the row it names is the one going away.
                // Written unconditionally, it erased the pointer whenever somebody
                // deleted a comment from the middle of a discussion - and the topic
                // list reads a topic's activity through that pointer, so the topic
                // dropped to the bottom of the activity order until the next comment.
                await _dbContext.Topics
                    .Where(t => t.TopicId == deleteComment.TopicId &&
                                t.LastCommentId == deleteComment.CommentId)
                    .ExecuteUpdateAsync(t => t.SetProperty(x => x.LastCommentId, successor));
            }

            await transaction.CommitAsync();
        });

        return removed;
    }
}
