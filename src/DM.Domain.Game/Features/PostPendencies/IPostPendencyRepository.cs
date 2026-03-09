using System;
using System.Threading.Tasks;
using DM.Domain.Game.Features.Games;

namespace DM.Domain.Game.Features.PostPendencies;

/// <summary>
/// Repository for post pendency operations
/// </summary>
public interface IPostPendencyRepository
{
    /// <summary>
    /// Get post pendency by ID
    /// </summary>
    Task<PostPendency?> Get(Guid pendencyId);

    /// <summary>
    /// Create post pendency
    /// </summary>
    Task<PostPendency> Create(CreatePostPendencyEntity entity);

    /// <summary>
    /// Delete post pendency
    /// </summary>
    Task Delete(Guid pendencyId);
}
