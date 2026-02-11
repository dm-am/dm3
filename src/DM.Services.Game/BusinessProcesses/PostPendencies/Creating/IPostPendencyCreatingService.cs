using System.Threading.Tasks;
using DM.Services.Game.Dto.Input;
using DM.Services.Game.Dto.Output;

namespace DM.Services.Game.BusinessProcesses.PostPendencies.Creating;

/// <summary>
/// Service for creating post pendencies
/// </summary>
public interface IPostPendencyCreatingService
{
    /// <summary>
    /// Create new post pendency
    /// </summary>
    /// <param name="createPostPendency">DTO model</param>
    /// <returns></returns>
    Task<PostPendency> Create(CreatePostPendency createPostPendency);
}
