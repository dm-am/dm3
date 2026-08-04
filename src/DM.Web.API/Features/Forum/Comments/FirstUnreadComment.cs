using System;

namespace DM.Web.API.Features.Forum.Comments;

/// <summary>
/// Where a reader continues in a topic
/// </summary>
public class FirstUnreadComment
{
    /// <summary>
    /// Comment to open the topic at: the first one the reader has not read, or
    /// the last comment of the topic when everything is read.
    /// Null when the topic has no comments at all
    /// </summary>
    public Guid? CommentId { get; set; }

    /// <summary>
    /// Position of that comment in the topic (1-based), for paging
    /// </summary>
    public int CommentNumber { get; set; }
}
