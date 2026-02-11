using System.Threading.Tasks;
using DM.Services.Game.Dto.Input;
using DM.Services.Game.Dto.Output;

namespace DM.Services.Game.BusinessProcesses.Posts.Creating;

/// <summary>
/// Service for post creating
/// </summary>
public interface IPostCreatingService
{
    /// <summary>
    /// Create new post
    /// </summary>
    /// <param name="createPost">DTO model</param>
    /// <returns></returns>
    Task<Post> Create(CreatePost createPost);
}