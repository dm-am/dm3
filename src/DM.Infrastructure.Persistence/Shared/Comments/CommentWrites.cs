using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Comments;
using Microsoft.EntityFrameworkCore;

namespace DM.Infrastructure.Persistence.Shared.Comments;

/// <summary>
/// The writing side of a comment, for all four discussions at once.
/// </summary>
/// <remarks>
/// The reading side moved here first (<see cref="CommentQueries" />) and the edit
/// trace after it (<see cref="CommentEdits" />); the write that ties the two
/// together stayed behind in four repositories, word for word, comment included.
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

        return await dbContext.Comments
            .TagWith(queryTag)
            .Where(c => c.CommentId == commentId)
            .ProjectToComment()
            .FirstAsync(ct);
    }
}
