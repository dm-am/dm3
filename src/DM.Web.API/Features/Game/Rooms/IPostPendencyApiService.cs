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
    /// <param name="postPendency">API DTO model</param>
    /// <returns>Envelope containing the created post pendency</returns>
    Task<Envelope<PostPendency>> Create(Guid roomId, PostPendency postPendency);

    /// <summary>
    /// Delete existing post pendency
    /// </summary>
    /// <param name="postPendencyId">Post pendency identifier</param>
    Task Delete(Guid postPendencyId);
}
