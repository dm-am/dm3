using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Comments;
using DM.Domain.Core.Dto;

namespace DM.Domain.Forum.Features.Comments;

public interface ITopicCommentService
{
    Task<Comment> CreateAsync(CreateComment createComment);

    /// <summary>
    /// Whether the current user may write a new comment under the topic: true
    /// exactly when <see cref="CreateAsync" /> takes one, the text aside. The
    /// page offers the form by this answer instead of refusing what was typed.
    /// </summary>
    /// <param name="topic">The topic, read for the current user</param>
    bool MayCreate(Topics.Topic topic);

    Task<(IEnumerable<Comment> Comments, PagingResult Paging)> GetAsync(Guid topicId, CommentsQuery query,
        IReadOnlyCollection<Guid>? excludeUserIds = null);

    /// <summary>
    /// Get a single comment of a topic by ID, refused the way the topic's
    /// comment listing refuses the current user. A comment of any other kind of
    /// discussion is not found
    /// </summary>
    Task<Comment> GetAsync(Guid commentId);

    Task<Comment> UpdateAsync(UpdateComment updateComment);

    Task DeleteAsync(Guid commentId);

    /// <summary>
    /// Find where the reader continues in a topic: the first comment he has not
    /// read, or the topic's last comment when everything is read
    /// </summary>
    Task<FirstUnreadComment> GetFirstUnreadAsync(string boardAlias, int topicNumber,
        IReadOnlyCollection<Guid>? excludeUserIds = null);

    /// <summary>
    /// Mark all topic comments as read
    /// </summary>
    Task MarkAsReadAsync(Guid topicId);

    /// <summary>
    /// Mark all comments in a board as read
    /// </summary>
    Task MarkBoardAsReadAsync(string boardTitle);

    /// <summary>
    /// Mark all forum comments as read (across all boards)
    /// </summary>
    Task MarkAllAsReadAsync();
}
