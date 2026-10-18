using System;
using System.Threading.Tasks;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Game.Rooms;

/// <summary>
/// API service for post pendency
/// </summary>
public interface IPostPendencyApiService
{
    /// <summary>
    /// Create new post pendency
    /// </summary>
    /// <param name="roomId">Room identifier</param>
    /// <param name="request">Character whose turn it is to post</param>
    /// <returns>Envelope containing the created post pendency</returns>
    Task<Envelope<PostPendency>> Create(Guid roomId, CreatePostPendencyRequest request);

    /// <summary>
    /// Delete existing post pendency
    /// </summary>
    /// <param name="postPendencyId">Post pendency identifier</param>
    Task Delete(Guid postPendencyId);
}
