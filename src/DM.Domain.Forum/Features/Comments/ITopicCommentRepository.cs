using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Comments;
using DM.Domain.Core.Dto;

namespace DM.Domain.Forum.Features.Comments;

public interface ITopicCommentRepository
{
    Task<int> Count(Guid topicId, CommentsQuery query, IReadOnlyCollection<Guid>? excludeUserIds = null);

    Task<IEnumerable<Comment>> Get(Guid topicId, CommentsQuery query, PagingData paging, IReadOnlyCollection<Guid>? excludeUserIds = null);

    Task<Comment?> Get(Guid commentId);

    /// <summary>
    /// Find the first comment of the topic posted after <paramref name="lastReadUtc" />,
    /// the moment the reader last marked the topic as read
    /// </summary>
    Task<FirstUnreadComment?> FindFirstUnread(Guid topicId, DateTimeOffset lastReadUtc,
        IReadOnlyCollection<Guid>? excludeUserIds = null);

    Task<FirstUnreadComment?> GetLastComment(Guid topicId,
        IReadOnlyCollection<Guid>? excludeUserIds = null);

    Task<Comment> Create(CreateTopicCommentEntity createComment);

    Task<Comment> Update(UpdateTopicCommentEntity updateComment);

    /// <summary>
    /// Get comment for deletion with topic info
    /// </summary>
    Task<TopicCommentToDelete?> GetForDelete(Guid commentId);

    /// <summary>
    /// Delete comment (soft delete). Answers whether this call is the removal that
    /// happened.
    /// </summary>
    /// <remarks>
    /// The topic's last-comment pointer moves inside the same transaction, and which
    /// comment it moves to is chosen there rather than handed in: a comment written
    /// between the caller's read and this write would otherwise be passed over, and
    /// the pointer would go back to a comment older than the discussion's newest.
    ///
    /// A caller with counters or events of its own reads the answer and does not
    /// repeat them for a comment somebody else removed first.
    /// </remarks>
    Task<bool> Delete(DeleteTopicCommentEntity deleteComment);
}

public class CreateTopicCommentEntity
{
    public Guid TopicId { get; set; }

    public Guid AuthorId { get; set; }

    public string Text { get; set; } = null!;
}

public class UpdateTopicCommentEntity
{
    public Guid CommentId { get; set; }

    public string Text { get; set; } = null!;

    public DateTimeOffset LastUpdateUtc { get; set; }

    public Guid EditorUserId { get; set; }
}

public class TopicCommentToDelete : Comment
{
    public Guid TopicId => EntityId;
}

public class DeleteTopicCommentEntity
{
    public Guid CommentId { get; set; }

    public Guid TopicId { get; set; }

    public Guid DeletedByUserId { get; set; }

    public DateTimeOffset DeletedUtc { get; set; }
}

/// <summary>
/// Where a reader continues in a topic: a comment and its place in the order
/// the discussion is paged by, so the caller can open the page holding it and
/// scroll to the comment itself
/// </summary>
public class FirstUnreadComment
{
    /// <summary>
    /// Absent when the topic has no comments at all
    /// </summary>
    public Guid? CommentId { get; set; }

    /// <summary>
    /// Position of that comment in the topic (1-based), for paging
    /// </summary>
    public int CommentNumber { get; set; }
}
