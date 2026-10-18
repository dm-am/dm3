using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Comments;
using DM.Infrastructure.Persistence.RelationalStorage;
using Microsoft.EntityFrameworkCore;
using DbComment = DM.Infrastructure.Persistence.Entities.Shared.Comment;

namespace DM.Infrastructure.Persistence.Shared.Comments;

/// <summary>
/// The writing side of a comment, for all four discussions at once.
/// </summary>
/// <remarks>
/// The reading side moved here first (<see cref="CommentQueries" />) and the edit
/// trace after it (<see cref="CommentEdits" />); the write that ties the two
/// together stayed behind in four repositories, word for word, comment included.
///
/// Three of the four are served by <see cref="Create{TOwner}" /> and
/// <see cref="Delete{TOwner}" />: a game, a blog and a publication differ only in
/// which row carries the discussion's comment count and last-comment pointer, so
/// the owner is named by the caller as a set, a key predicate and a column
/// selector apiece, and everything else is stated once: the order of the
/// statements, the transaction around them, the condition each write is guarded
/// by. The one thing a caller still spells out is the table its pointer lives
/// in, because that half of the move is raw SQL and a raw statement has to name
/// its table. The forum topic keeps its own copy of both, in the same form and
/// for one reason: it carries no comment count for these to raise and lower, so
/// the statement that takes the owner row here would have nothing to write, and
/// the topic takes its row by name instead.
///
/// The query tag stays with the caller, for the reason it does on the reading
/// side: it is what names the module a slow query came from.
/// </remarks>
internal static class CommentWrites
{
    /// <summary>
    /// Replace the text of a comment, record who changed it, and answer with the
    /// comment as it now reads.
    /// </summary>
    /// <remarks>
    /// The comment row keeps no modification stamp: ModifiedUtc is derived from
    /// the newest entry of the edit history, and the client draws its "edited"
    /// mark from that. The trace is written here rather than at the call site so
    /// the text and its trace go in one SaveChanges.
    ///
    /// A comment that is not there is not an error: the read below answers for
    /// that, exactly as the four copies did.
    ///
    /// The answer carries the likes: the client replaces the comment it holds with
    /// it, and an edit must not empty the heart under it.
    /// </remarks>
    public static async Task<Comment> Update(
        DmDbContext dbContext,
        IGuidFactory guidFactory,
        Guid commentId,
        string text,
        Guid editorUserId,
        DateTimeOffset editedUtc,
        string queryTag,
        CancellationToken ct = default)
    {
        var dbComment = await dbContext.Comments.FindAsync([commentId], ct);
        if (dbComment != null)
        {
            dbComment.Text = text;
            CommentEdits.Record(dbContext, guidFactory, commentId, editorUserId, editedUtc);
            await dbContext.SaveChangesAsync(ct);
        }

        var updated = await dbContext.Comments
            .TagWith(queryTag)
            .Where(c => c.CommentId == commentId)
            .ProjectToComment()
            .FirstAsync(ct);

        await CommentQueries.FillLikes(dbContext, [updated], queryTag, ct);
        return updated;
    }

    /// <summary>
    /// Add a comment to a discussion: the owner's count, the row itself and the
    /// last-comment pointer, in one transaction.
    /// </summary>
    /// <remarks>
    /// The count is raised in the database instead of being assigned a number the
    /// caller read a query earlier: two comments written to one discussion at once
    /// both read the same count and both wrote count + 1, so one of the two was
    /// never counted and nothing recomputes the column afterwards. That statement
    /// is also what takes the owner row, so the pointer below decides against a
    /// state no other writer can move until commit.
    ///
    /// The strategy wrapper is required because the API host configures
    /// EnableRetryOnFailure, and the three statements are one unit.
    /// </remarks>
    /// <typeparam name="TOwner">Entity the discussion hangs on</typeparam>
    /// <param name="dbContext">Storage context</param>
    /// <param name="owners">Set the discussion's owner lives in</param>
    /// <param name="owner">Which row of that set owns this discussion</param>
    /// <param name="commentCount">The owner's stored comment count</param>
    /// <param name="ownerId">Key of the owning row, as the pointer statement names it</param>
    /// <param name="comment">The comment to write</param>
    /// <param name="moveLastCommentForward">
    /// The owner's pointer statement, taking the owner as {0}, the new comment as
    /// {1} and its timestamp as {2}. Written by the caller because a raw statement
    /// has to name its table; what it has to say is stated under
    /// <see cref="MoveLastCommentForward" />.
    /// </param>
    /// <param name="queryTag">Tag naming the module the read-back belongs to</param>
    /// <param name="ct">Cancellation token</param>
    public static async Task<Comment> Create<TOwner>(
        DmDbContext dbContext,
        DbSet<TOwner> owners,
        Expression<Func<TOwner, bool>> owner,
        Expression<Func<TOwner, int>> commentCount,
        Guid ownerId,
        DbComment comment,
        string moveLastCommentForward,
        string queryTag,
        CancellationToken ct = default)
        where TOwner : class
    {
        await RetryableWrite.Run(dbContext, async cancellation =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellation);

            await owners
                .Where(owner)
                .ExecuteUpdateAsync(o => o.SetProperty(commentCount, Raised(commentCount)), cancellation);

            dbContext.Comments.Add(comment);
            await dbContext.SaveChangesAsync(cancellation);

            await MoveLastCommentForward(
                dbContext, moveLastCommentForward,
                ownerId, comment.CommentId, comment.CreatedUtc, cancellation);

            await transaction.CommitAsync(cancellation);
        }, ct);

        return await dbContext.Comments
            .TagWith(queryTag)
            .Where(c => c.CommentId == comment.CommentId)
            .ProjectToComment()
            .FirstAsync(ct);
    }

    /// <summary>
    /// Take a comment out of a discussion: the mark, the owner's count and the
    /// last-comment pointer, in one transaction. Answers whether this call is the
    /// removal that happened.
    /// </summary>
    /// <remarks>
    /// One statement reads and writes the comment row: a second removal of the
    /// same comment arriving together with this one waits on the row lock and then
    /// re-tests the predicate against the committed state, so exactly one of the
    /// two matches and everything below it happens once. A caller with counters or
    /// events of its own reads the answer and does not repeat them for a comment
    /// somebody else removed first.
    ///
    /// The successor is chosen here, inside the transaction and after the mark,
    /// rather than handed in by a caller that read it a statement earlier: a
    /// comment written in between would otherwise be passed over and the pointer
    /// would go back to a comment older than the discussion's newest. The read
    /// cannot pick the row this transaction has just marked: it is removed as far
    /// as this transaction is concerned, and the discussion query drops removed
    /// rows. It is named as excluded regardless.
    /// </remarks>
    /// <typeparam name="TOwner">Entity the discussion hangs on</typeparam>
    /// <param name="dbContext">Storage context</param>
    /// <param name="owners">Set the discussion's owner lives in</param>
    /// <param name="owner">Which row of that set owns this discussion</param>
    /// <param name="ownerNamingComment">
    /// The same row while its pointer still names the comment being removed: the
    /// pointer moves for the last comment of a discussion and for no other.
    /// </param>
    /// <param name="commentCount">The owner's stored comment count</param>
    /// <param name="lastComment">The owner's last-comment pointer</param>
    /// <param name="ownerId">Discussion the successor is looked for in</param>
    /// <param name="commentId">Comment to remove</param>
    /// <param name="deletedByUserId">Whose hand the removal is recorded under</param>
    /// <param name="deletedUtc">When the removal happened</param>
    /// <param name="queryTag">Tag naming the module the successor read belongs to</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Whether this call is the one that removed the comment</returns>
    public static async Task<bool> Delete<TOwner>(
        DmDbContext dbContext,
        DbSet<TOwner> owners,
        Expression<Func<TOwner, bool>> owner,
        Expression<Func<TOwner, bool>> ownerNamingComment,
        Expression<Func<TOwner, int>> commentCount,
        Expression<Func<TOwner, Guid?>> lastComment,
        Guid ownerId,
        Guid commentId,
        Guid deletedByUserId,
        DateTimeOffset deletedUtc,
        string queryTag,
        CancellationToken ct = default)
        where TOwner : class
    {
        var removed = false;

        await RetryableWrite.Run(dbContext, async cancellation =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellation);

            var marked = await dbContext.Comments
                .Where(c => c.CommentId == commentId && !c.IsRemoved)
                .ExecuteUpdateAsync(c => c
                    .SetProperty(x => x.IsRemoved, true)
                    .SetProperty(x => x.DeletedByUserId, (Guid?)deletedByUserId)
                    .SetProperty(x => x.DeletedUtc, (DateTimeOffset?)deletedUtc), cancellation);

            removed = marked == 1;
            if (removed)
            {
                // Floored at zero rather than trusted: the column is stored and
                // nothing recomputes it, so a count that has already drifted below
                // its discussion must not go negative and stay there.
                await owners
                    .Where(owner)
                    .ExecuteUpdateAsync(
                        o => o.SetProperty(commentCount, Lowered(commentCount)), cancellation);

                var successor = await CommentQueries.NewestExcept(
                    dbContext, ownerId, commentId, queryTag, cancellation);

                await owners
                    .Where(ownerNamingComment)
                    .ExecuteUpdateAsync(o => o.SetProperty(lastComment, successor), cancellation);
            }

            await transaction.CommitAsync(cancellation);
        }, ct);

        return removed;
    }

    /// <summary>
    /// Point the owner at the new comment unless it already names a newer one.
    /// </summary>
    /// <remarks>
    /// CreatedUtc is stamped before the row lock is taken, so the comment that
    /// commits last is not necessarily the newest one, and an unconditional
    /// assignment sent the discussion's last activity backwards. Kept out of the
    /// increment on purpose: one statement doing both would move the pointer for
    /// every comment.
    ///
    /// The condition is stated as "the owner names nothing newer" rather than as a
    /// list of the cases in which the pointer may move. A pointer that is null
    /// names nothing; a pointer left over from a row that is no longer in the
    /// table names nothing either, no foreign key standing behind the column, and
    /// both have to give way to a comment that is really there.
    ///
    /// Written out in SQL because the tie-break is over an identifier: two
    /// comments can share a timestamp, and Guid carries no ordering operator in C#
    /// to express that with. The pair comparison is the same order the successor
    /// query reads by, so creation and deletion agree on which of two simultaneous
    /// comments is the newer.
    /// </remarks>
    private static Task MoveLastCommentForward(
        DmDbContext dbContext,
        string statement,
        Guid ownerId,
        Guid commentId,
        DateTimeOffset createdUtc,
        CancellationToken ct) =>
        dbContext.Database.ExecuteSqlRawAsync(statement, [ownerId, commentId, createdUtc], ct);

    /// <summary>The same counter, one higher.</summary>
    private static Expression<Func<TOwner, int>> Raised<TOwner>(
        Expression<Func<TOwner, int>> counter) =>
        Expression.Lambda<Func<TOwner, int>>(
            Expression.Add(counter.Body, Expression.Constant(1)), counter.Parameters);

    /// <summary>The same counter, one lower but never below zero.</summary>
    private static Expression<Func<TOwner, int>> Lowered<TOwner>(
        Expression<Func<TOwner, int>> counter) =>
        Expression.Lambda<Func<TOwner, int>>(
            Expression.Condition(
                Expression.GreaterThan(counter.Body, Expression.Constant(0)),
                Expression.Subtract(counter.Body, Expression.Constant(1)),
                Expression.Constant(0)),
            counter.Parameters);
}
