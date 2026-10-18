using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Comments;
using DM.Domain.Core.Dto;

namespace DM.Domain.Forum.Features.Comments;

/// <summary>
/// Repository for topic comments
/// </summary>
public interface ITopicCommentRepository
{
    /// <summary>
    /// Count comments of the topic with optional filtering
    /// </summary>
    /// <param name="topicId">Topic identifier</param>
    /// <param name="query">Query with search and author filter</param>
    /// <param name="excludeUserIds">Optional user IDs to exclude from count</param>
    Task<int> Count(Guid topicId, CommentsQuery query, IReadOnlyCollection<Guid>? excludeUserIds = null);

    /// <summary>
    /// Get comments list of the topic with filtering and sorting
    /// </summary>
    /// <param name="topicId">Topic identifier</param>
    /// <param name="query">Query with search, author filter, and sorting</param>
    /// <param name="paging">Paging data</param>
    /// <param name="excludeUserIds">Optional user IDs to exclude from results</param>
    Task<IEnumerable<Comment>> Get(Guid topicId, CommentsQuery query, PagingData paging, IReadOnlyCollection<Guid>? excludeUserIds = null);

    /// <summary>
    /// Get single comment by its identifier
    /// </summary>
    Task<Comment?> Get(Guid commentId);

    /// <summary>
    /// Find the first comment of the topic the reader has not seen yet
    /// </summary>
    /// <param name="topicId">Topic identifier</param>
    /// <param name="lastReadUtc">Moment the reader last marked the topic as read</param>
    /// <param name="excludeUserIds">Optional user IDs hidden from this reader</param>
    Task<FirstUnreadComment?> FindFirstUnread(Guid topicId, DateTimeOffset lastReadUtc,
        IReadOnlyCollection<Guid>? excludeUserIds = null);

    /// <summary>
    /// Get the last comment of the topic
    /// </summary>
    /// <param name="topicId">Topic identifier</param>
    /// <param name="excludeUserIds">Optional user IDs hidden from this reader</param>
    Task<FirstUnreadComment?> GetLastComment(Guid topicId,
        IReadOnlyCollection<Guid>? excludeUserIds = null);

    /// <summary>
    /// Create comment for topic
    /// </summary>
    /// <param name="createComment">Comment creation data</param>
    /// <returns>Created comment</returns>
    Task<Comment> Create(CreateTopicCommentEntity createComment);

    /// <summary>
    /// Update comment
    /// </summary>
    /// <param name="updateComment">Comment update data</param>
    /// <returns>Updated comment</returns>
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
    /// <param name="deleteComment">Comment deletion data</param>
    /// <returns>True when this call is the one that removed the comment.</returns>
    Task<bool> Delete(DeleteTopicCommentEntity deleteComment);
}

/// <summary>
/// DTO for creating a topic comment (repository layer)
/// </summary>
public class CreateTopicCommentEntity
{
    /// <summary>
    /// Topic identifier
    /// </summary>
    public Guid TopicId { get; set; }

    /// <summary>
    /// Comment author identifier
    /// </summary>
    public Guid AuthorId { get; set; }

    /// <summary>
    /// Comment text
    /// </summary>
    public string Text { get; set; } = null!;

}

/// <summary>
/// DTO for updating a topic comment (repository layer)
/// </summary>
public class UpdateTopicCommentEntity
{
    /// <summary>
    /// Comment identifier
    /// </summary>
    public Guid CommentId { get; set; }

    /// <summary>
    /// Updated comment text
    /// </summary>
    public string Text { get; set; } = null!;

    /// <summary>
    /// Last edit timestamp
    /// </summary>
    public DateTimeOffset LastUpdateUtc { get; set; }

    /// <summary>
    /// User performing the edit
    /// </summary>
    public Guid EditorUserId { get; set; }
}

/// <summary>
/// DTO to remove topic comment
/// </summary>
public class TopicCommentToDelete : Comment
{
    /// <summary>
    /// Topic identifier
    /// </summary>
    public Guid TopicId => EntityId;
}

/// <summary>
/// DTO for deleting a topic comment (repository layer)
/// </summary>
public class DeleteTopicCommentEntity
{
    /// <summary>
    /// Comment identifier
    /// </summary>
    public Guid CommentId { get; set; }

    /// <summary>
    /// Topic identifier
    /// </summary>
    public Guid TopicId { get; set; }

    /// <summary>
    /// User who removed the comment
    /// </summary>
    public Guid DeletedByUserId { get; set; }

    /// <summary>
    /// When the comment was removed
    /// </summary>
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
    /// Comment identifier; absent when the topic has no comments at all
    /// </summary>
    public Guid? CommentId { get; set; }

    /// <summary>
    /// Position of that comment in the topic (1-based), for paging
    /// </summary>
    public int CommentNumber { get; set; }
}
