using DM.Domain.Game.Features.Games;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;


namespace DM.Domain.Game.Features.Posts;

/// <summary>
/// Unified service for post CRUD operations
/// </summary>
public interface IPostService
{
    #region Create

    /// <summary>
    /// Create new post
    /// </summary>
    /// <param name="createPost">DTO model</param>
    Task<Post> CreateAsync(CreatePost createPost);

    #endregion

    #region Read

    /// <summary>
    /// Get list of posts in the room
    /// </summary>
    /// <param name="roomId">Room identifier</param>
    /// <param name="query">Paging query</param>
    Task<(IEnumerable<Post> posts, PagingResult paging)> GetAllAsync(Guid roomId, PagingQuery query);

    /// <summary>
    /// Get single existing post
    /// </summary>
    /// <param name="postId">Post identifier</param>
    Task<Post> GetAsync(Guid postId);

    /// <summary>
    /// Mark all posts in a room as read
    /// </summary>
    Task MarkAsReadAsync(Guid roomId);

    /// <summary>
    /// Get best post (highest rated) by user from open rooms
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <returns>Best post result or null if no posts with positive rating found</returns>
    Task<BestPostResult?> GetBestPostAsync(Guid userId);

    /// <summary>
    /// Get posts with rating info (global search with filters and sorting)
    /// </summary>
    Task<(IEnumerable<Post> Posts, PagingResult Paging)> GetRatedAsync(PostsQuery query);

    #endregion

    #region Update

    /// <summary>
    /// Update existing post
    /// </summary>
    /// <param name="updatePost">DTO for post updating</param>
    Task<Post> UpdateAsync(UpdatePost updatePost);

    #endregion

    #region Delete

    /// <summary>
    /// Delete existing post (soft delete)
    /// </summary>
    /// <param name="postId">Post identifier</param>
    Task DeleteAsync(Guid postId);

    #endregion
}
