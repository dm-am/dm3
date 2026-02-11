using System;
using System.Threading.Tasks;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Dto.Games;

namespace DM.Web.API.Services.Game.Rooms;

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
    /// <returns></returns>
    Task<Envelope<PostPendency>> Create(Guid roomId, PostPendency postPendency);

    /// <summary>
    /// Delete existing post pendency
    /// </summary>
    /// <param name="postPendencyId">Post pendency identifier</param>
    /// <returns></returns>
    Task Delete(Guid postPendencyId);
}