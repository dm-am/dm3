using System;
using DM.Services.DataAccess.BusinessObjects.Games.Posts;
using DM.Services.Game.Dto.Input;

namespace DM.Services.Game.BusinessProcesses.Posts.Creating;

/// <summary>
/// Factory for post DAL model
/// </summary>
internal interface IPostFactory
{
    /// <summary>
    /// Create new game post
    /// </summary>
    /// <param name="createPost">DTO model</param>
    /// <param name="userId">User identifier</param>
    /// <returns></returns>
    Post Create(CreatePost createPost, Guid userId);
}