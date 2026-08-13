using System;
using DM.Domain.Core.Abstractions;
using DbCommentEdit = DM.Infrastructure.Persistence.Entities.Shared.CommentEdit;

namespace DM.Infrastructure.Persistence.Shared.Comments;

/// <summary>
/// The writing side of the comment edit history, for all four discussions at once.
/// </summary>
/// <remarks>
/// A comment row keeps no modification stamp of its own: the mapping profile
/// derives ModifiedUtc from the most recent entry here, and the client draws its
/// "edited" mark from that. Nothing wrote an entry — all four repositories said
/// tracking was "handled via Edit history" and then saved the new text alone — so
/// the field was null for every comment ever edited, and an edit was invisible to
/// the reader. The forum's topic history is the same shape and has always been
/// written; this is the comment side of it.
///
/// Shared rather than copied, for the reason the reading side is shared: four
/// copies of a rule are four chances to fix it in three places.
/// </remarks>
internal static class CommentEdits
{
    /// <summary>
    /// Record who edited a comment and when.
    /// </summary>
    /// <remarks>
    /// Added to the tracker, not saved: the caller is mid-write and owns the
    /// SaveChanges, so the text and its trace go in one round trip and one cannot
    /// land without the other.
    /// </remarks>
    /// <param name="dbContext">Context of the ongoing write</param>
    /// <param name="guidFactory">Identifier source of the host</param>
    /// <param name="commentId">Comment being edited</param>
    /// <param name="editorId">User performing the edit</param>
    /// <param name="editedUtc">Moment of the edit, as the caller stamped it</param>
    public static void Record(
        DmDbContext dbContext,
        IGuidFactory guidFactory,
        Guid commentId,
        Guid editorId,
        DateTimeOffset editedUtc)
    {
        // An empty editor is a caller that did not say who is editing. Writing the
        // trace anyway would put Guid.Empty in a column with a foreign key to
        // Users, which fails the insert and takes the edit down with it.
        if (editorId == Guid.Empty)
        {
            return;
        }

        dbContext.CommentEdits.Add(new DbCommentEdit
        {
            CommentEditId = guidFactory.Create(),
            CommentId = commentId,
            EditorUserId = editorId,
            EditedUtc = editedUtc
        });
    }
}
