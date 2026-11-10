using System;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;

namespace DM.Domain.Blog.Features.Likes;

/// <summary>
/// Unified service for all blog-related likes
/// </summary>
public interface IBlogLikeService
{
    Task<GeneralUser> LikeBlogCommentAsync(Guid commentId);

    Task UnlikeBlogCommentAsync(Guid commentId);

    Task<GeneralUser> LikePublicationCommentAsync(Guid commentId);

    Task UnlikePublicationCommentAsync(Guid commentId);

    Task<GeneralUser> LikePublicationAsync(Guid publicationId);

    Task UnlikePublicationAsync(Guid publicationId);
}
