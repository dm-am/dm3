using System.Threading.Tasks;
using DM.Services.DataAccess.RelationalStorage;
using DM.Services.Game.Dto.Output;
using DbPost = DM.Services.DataAccess.BusinessObjects.Games.Posts.Post;

namespace DM.Services.Game.BusinessProcesses.Posts.Updating;

/// <summary>
/// Storage for post updating
/// </summary>
internal interface IPostUpdatingRepository
{
    /// <summary>
    /// Update post
    /// </summary>
    /// <param name="updatePost">Update post</param>
    /// <returns></returns>
    Task<Post?> Update(IUpdateBuilder<DbPost> updatePost);
}