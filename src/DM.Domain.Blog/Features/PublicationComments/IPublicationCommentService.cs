using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Comments;
using DM.Domain.Core.Dto;

namespace DM.Domain.Blog.Features.PublicationComments;

public interface IPublicationCommentService
{
    Task<Comment> CreateAsync(CreateComment createComment);

    /// <summary>
    /// Whether the current user may write a new comment under the publication:
    /// true exactly when <see cref="CreateAsync" /> takes one, the text aside.
    /// The page offers the form by this answer instead of refusing what was
    /// typed.
    /// </summary>
    /// <param name="publication">The publication, read for the current user</param>
    Task<bool> MayCreateAsync(Publications.Publication publication);

    Task<(IEnumerable<Comment> Comments, PagingResult Paging)> GetAsync(Guid publicationId, CommentsQuery query,
        IReadOnlyCollection<Guid>? excludeUserIds = null);

    /// <summary>
    /// Get a single comment of a publication by ID, refused the way the publication's
    /// comment listing refuses the current user. A comment of any other kind of
    /// discussion is not found
    /// </summary>
    Task<Comment> GetAsync(Guid commentId);

    Task<Comment> UpdateAsync(UpdateComment updateComment);

    Task DeleteAsync(Guid commentId);

    /// <summary>
    /// Mark all publication comments as read
    /// </summary>
    Task MarkAsReadAsync(Guid publicationId);
}
