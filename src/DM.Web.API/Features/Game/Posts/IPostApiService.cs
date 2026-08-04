using System;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Game.Features.Posts;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Game.Posts;

/// <summary>
/// API service for game posts
/// </summary>
public interface IPostApiService
{
    /// <summary>
    /// Get room posts
    /// </summary>
    /// <param name="roomId">Room identifier</param>
    /// <param name="query">Search query</param>
    /// <returns>List envelope containing room posts</returns>
    Task<ListEnvelope<Post>> Get(Guid roomId, PagingQuery query);

    /// <summary>
    /// Get single post
    /// </summary>
    /// <param name="postId">Post identifier</param>
    /// <returns>Envelope containing the post</returns>
    Task<Envelope<Post>> Get(Guid postId);

    /// <summary>
    /// Create new post
    /// </summary>
    /// <param name="roomId">Room identifier</param>
    /// <param name="post">Post creation request</param>
    /// <returns>Envelope containing the created post</returns>
    Task<Envelope<Post>> Create(Guid roomId, CreatePostRequest post);

    /// <summary>
    /// Update existing post
    /// </summary>
    /// <param name="postId">Post identifier</param>
    /// <param name="request">Editable post fields</param>
    /// <returns>Envelope containing the updated post</returns>
    Task<Envelope<Post>> Update(Guid postId, UpdatePostRequest request);

    /// <summary>
    /// Delete existing post
    /// </summary>
    /// <param name="postId">Post identifier</param>
    Task Delete(Guid postId);

    /// <summary>
    /// Mark all room posts as read
    /// </summary>
    Task MarkAsRead(Guid roomId);

    /// <summary>
    /// Get posts with rating info
    /// </summary>
    Task<ListEnvelope<Post>> GetRated(PostsQuery query);
}
