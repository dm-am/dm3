using System;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Core.Exceptions;

namespace DM.Domain.Core.Comments;

/// <summary>
/// How a comment read by its id answers when its discussion is not there to the
/// reader.
/// </summary>
/// <remarks>
/// The four discussions share one table of comments, and a comment is read by
/// its id alone before its discussion is asked. The discussion answers in its own
/// words: the topic or the game is not found, or is gone, or the publication is
/// not found. Passed on as they are, those answers told a stranger that the id
/// is a comment, and of which discussion - an oracle next to the plain "no such
/// comment" the read gives for an id that is nothing. So a discussion that is not
/// there to the reader answers exactly as a comment that is not there, by the
/// same sentence. A refusal of another kind - the reader sees the discussion and
/// may not read its comments - is not about existence and passes unchanged.
/// </remarks>
public static class DiscussionRefusal
{
    /// <summary>
    /// Read the discussion of a comment, answering "comment not found" where the
    /// discussion is not found or gone.
    /// </summary>
    /// <param name="commentId">The comment the reader asked for</param>
    /// <param name="readDiscussion">The read of its discussion, gated as the listing is</param>
    public static async Task<T> AsCommentNotFound<T>(Guid commentId, Func<Task<T>> readDiscussion)
    {
        try
        {
            return await readDiscussion();
        }
        catch (HttpException refusal) when (refusal.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.CommentNotFound(commentId));
        }
    }
}
