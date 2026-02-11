using System.Threading.Tasks;
using DM.Services.Game.Dto.Input;
using DM.Services.Game.Dto.Output;

namespace DM.Services.Game.BusinessProcesses.Posts.Updating;

/// <summary>
/// Service for post updating
/// </summary>
public interface IPostUpdatingService
{
    /// <summary>
    /// Update existing post
    /// </summary>
    /// <param name="updatePost">DTO for post updating</param>
    /// <returns></returns>
    Task<Post> Update(UpdatePost updatePost);
}