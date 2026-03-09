using DM.Domain.Game.Features.Games;
using System;
using System.Threading.Tasks;


namespace DM.Domain.Game.Features.PostPendencies;

/// <summary>
/// Unified service for post pendency CRUD operations
/// </summary>
public interface IPostPendencyService
{
    #region Create

    /// <summary>
    /// Create new post pendency
    /// </summary>
    /// <param name="createPostPendency">DTO model</param>
    Task<PostPendency> CreateAsync(CreatePostPendency createPostPendency);

    #endregion

    #region Delete

    /// <summary>
    /// Delete existing post pendency
    /// </summary>
    /// <param name="pendencyId">Post pendency identifier</param>
    Task DeleteAsync(Guid pendencyId);

    #endregion
}
