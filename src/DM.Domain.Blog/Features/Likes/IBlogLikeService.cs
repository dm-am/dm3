using System;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;

namespace DM.Domain.Blog.Features.Likes;

/// <summary>
/// Unified service for all blog-related likes
/// </summary>
public interface IBlogLikeService
{
    /// <returns>User who liked the comment</returns>
    Task<GeneralUser> LikeBlogCommentAsync(Guid commentId);

    Task UnlikeBlogCommentAsync(Guid commentId);

    /// <returns>User who liked the comment</returns>
    Task<GeneralUser> LikePublicationCommentAsync(Guid commentId);

    Task UnlikePublicationCommentAsync(Guid commentId);

    /// <returns>User who liked the publication</returns>
    Task<GeneralUser> LikePublicationAsync(Guid publicationId);

    Task UnlikePublicationAsync(Guid publicationId);
}
