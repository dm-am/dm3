using DM.Domain.Core.Authorization;
using DM.Domain.Core.Exceptions;

namespace DM.Domain.Core.Comments;

/// <summary>
/// How a comment read by its id answers when its discussion is not there to the
/// reader.
/// </summary>
/// <remarks>
/// The discussion's own "not found" would tell a stranger that the id is a comment
/// and of which discussion, so it answers as a comment that is not there.
/// </remarks>
public static class DiscussionRefusal
{
    /// <summary>
    /// Read the discussion of a comment, answering "comment not found" where the
    /// discussion is not found or gone.
    /// </summary>
    /// <param name="commentId">The comment the reader asked for</param>
    /// <param name="readDiscussion">The read of its discussion, gated as the listing is</param>
    public static Task<T> AsCommentNotFound<T>(Guid commentId, Func<Task<T>> readDiscussion) =>
        HiddenContainerRefusal.AsNotFound(RefusalMessage.CommentNotFound(commentId), readDiscussion);
}
