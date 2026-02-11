using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Services.Core.Dto;
using DM.Services.Game.Dto.Output;

namespace DM.Services.Game.BusinessProcesses.Posts.Reading;

/// <summary>
/// Storage for reading game posts
/// </summary>
internal interface IPostReadingRepository
{
    /// <summary>
    /// Count posts in a room
    /// </summary>
    /// <param name="roomId">Room identifier</param>
    /// <param name="userId">User identifier</param>
    /// <returns></returns>
    Task<int> Count(Guid roomId, Guid userId);

    /// <summary>
    /// Get room posts
    /// </summary>
    /// <param name="roomId">Room identifier</param>
    /// <param name="paging">Paging data</param>
    /// <param name="userId">User identifier</param>
    /// <returns></returns>
    Task<IEnumerable<Post>> Get(Guid roomId, PagingData paging, Guid userId);

    /// <summary>
    /// Get single room post
    /// </summary>
    /// <param name="postId">Post identifier</param>
    /// <param name="userId">User identifier</param>
    /// <returns></returns>
    Task<Post?> Get(Guid postId, Guid userId);

    /// <summary>
    /// Get best post (highest rated) by user from open rooms
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <returns>Best post result or null if no posts with positive rating found</returns>
    Task<BestPostResult?> GetBestPost(Guid userId);
}